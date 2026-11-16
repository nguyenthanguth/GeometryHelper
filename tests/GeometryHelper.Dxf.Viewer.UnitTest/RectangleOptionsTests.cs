using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// RECTANG with its options as AutoCAD 2021 asks and draws them (probe draw1, accoreconsole through standard input):
    /// Chamfer, Elevation, Fillet, Thickness and Width at the first prompt, Area, Dimensions and Rotation at the second, the
    /// modes kept for the next rectangle.
    /// </summary>
    public class RectangleOptionsTests
    {
        private static DxfPolyline Last(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        /// <summary>The vertices of a polyline as "x,y" to four places, space between: "10,10 40,10 40,30 10,30".</summary>
        private static string Vertices(DxfPolyline polyline) =>
            string.Join(
                " ",
                Enumerable
                    .Range(0, polyline.NumberOfVertices)
                    .Select(i =>
                    {
                        GeoPoint2 p = polyline.GetPoint2dAt(i);
                        return Number(p.X) + "," + Number(p.Y);
                    })
            );

        private static string Number(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.####", CultureInfo.InvariantCulture);

        [Fact]
        public void TheFirstPrompt_OffersTheFiveModes_AndTheSecondTheThree()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "RECTANG");
            Assert.Equal(
                "Specify first corner point or [Chamfer/Elevation/Fillet/Thickness/Width]:",
                session.PromptText
            );
            Run(session, P(10, 10));
            Assert.Equal("Specify other corner point or [Area/Dimensions/Rotation]:", session.PromptText);
        }

        [Fact]
        public void AChamferOfOneAndTwo_CutsEachCorner_TheFirstDistanceBackAlongTheSideThatComes()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "C");
            Assert.Equal("Specify first chamfer distance for rectangles <0.0000>:", session.PromptText);
            Run(session, "1");
            Assert.Equal("Specify second chamfer distance for rectangles <1.0000>:", session.PromptText);
            Run(session, "2");
            Assert.Equal(
                "Specify first corner point or [Chamfer/Elevation/Fillet/Thickness/Width]:",
                session.PromptText
            );
            Run(session, P(10, 10), P(40, 30));

            DxfPolyline rectangle = Last(document);
            Assert.True(rectangle.Closed);
            Assert.Equal("10,11 12,10 39,10 40,12 40,29 38,30 11,30 10,28", Vertices(rectangle));
            Assert.False(rectangle.HasBulges);
        }

        [Fact]
        public void AChamfer_TakenFromTheOtherCornerDownLeft_CutsTheSameWayRound()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "C", "1", "2", P(40, 30), P(10, 10));

            Assert.Equal("40,29 38,30 11,30 10,28 10,11 12,10 39,10 40,12", Vertices(Last(document)));
        }

        [Fact]
        public void AFilletOfTwo_RoundsEachCornerWithAQuarterArc_AndTurnsItsBulgeWithTheWinding()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "F", "2", P(10, 10), P(40, 30));
            DxfPolyline counterClockwise = Last(document);

            Assert.Equal("10,12 12,10 38,10 40,12 40,28 38,30 12,30 10,28", Vertices(counterClockwise));
            double quarter = Math.Tan(Math.PI / 8.0);
            Assert.Equal(quarter, counterClockwise.GetBulgeAt(0), 9);
            Assert.Equal(0.0, counterClockwise.GetBulgeAt(1), 9);
            Assert.Equal(quarter, counterClockwise.GetBulgeAt(2), 9);

            // From the top left to the bottom right it winds the other way, its arcs bulging the other way.
            Run(session, "RECTANG", P(10, 30), P(40, 10));
            DxfPolyline clockwise = Last(document);
            Assert.Equal(-quarter, clockwise.GetBulgeAt(0), 9);
            Assert.Equal("10,28 12,30 38,30 40,28 40,12 38,10 12,10 10,12", Vertices(clockwise));
        }

        [Fact]
        public void AChamferAndAFillet_ShutEachOtherOut_EachOfferingTheOthersFirstValue()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "C", "1", "2", "F");
            Assert.Equal("Specify fillet radius for rectangles <1.0000>:", session.PromptText);
            Run(session, "3", "C");
            Assert.Equal("Specify first chamfer distance for rectangles <3.0000>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify second chamfer distance for rectangles <3.0000>:", session.PromptText);
            Run(session, Enter, P(10, 10), P(40, 30));

            // The chamfer of 3 and 3 stood in the fillet's place and cleared it: no bulge.
            DxfPolyline rectangle = Last(document);
            Assert.False(rectangle.HasBulges);
            Assert.Equal("10,13 13,10 37,10 40,13 40,27 37,30 13,30 10,27", Vertices(rectangle));

            Run(session, "RECTANG");
            Assert.Contains("Current rectangle modes:  Chamfer=3.0000 x 3.0000", session.History);
        }

        [Fact]
        public void ADistanceTooLargeForTheSides_LeavesTheRectangleWithTwoVerticesAtEachCorner()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // A chamfer takes both its distances off the shorter side (20): 10 and 10 fit, 10 and 11 do not.
            Run(session, "RECTANG", "C", "10", "10", P(10, 10), P(40, 30));
            Assert.Equal("10,20 20,10 30,10 40,20 40,20 30,30 20,30 10,20", Vertices(Last(document)));

            Run(session, "RECTANG", "C", "10", "11", P(10, 10), P(40, 30));
            Assert.Equal("10,10 10,10 40,10 40,10 40,30 40,30 10,30 10,30", Vertices(Last(document)));

            // A fillet needs twice its radius: 10 fits, 11 does not.
            Run(session, "RECTANG", "F", "11", P(10, 10), P(40, 30));
            DxfPolyline plain = Last(document);
            Assert.Equal("10,10 10,10 40,10 40,10 40,30 40,30 10,30 10,30", Vertices(plain));
            Assert.False(plain.HasBulges);
        }

        [Fact]
        public void ElevationThicknessAndWidth_AreGivenToTheRectangle_AndKeptForTheNext()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "E");
            Assert.Equal("Specify the elevation for rectangles <0.0000>:", session.PromptText);
            Run(session, "5", "T");
            Assert.Equal("Specify thickness for rectangles <0.0000>:", session.PromptText);
            Run(session, "4", "W");
            Assert.Equal("Specify line width for rectangles <0.0000>:", session.PromptText);
            Run(session, "0.5", P(10, 10), P(40, 30));

            DxfPolyline rectangle = Last(document);
            Assert.Equal(5.0, rectangle.Elevation, 9);
            Assert.Equal(4.0, rectangle.Thickness, 9);
            Assert.Equal(0.5, rectangle.ConstantWidth, 9);
            Assert.Equal("10,10 40,10 40,30 10,30", Vertices(rectangle));

            Run(session, "RECTANG");
            Assert.Contains(
                "Current rectangle modes:  Elevation=5.0000  Thickness=4.0000  Width=0.5000",
                session.History
            );
            Run(session, "W");
            Assert.Equal("Specify line width for rectangles <0.5000>:", session.PromptText);
            Run(session, Enter, P(50, 10), P(60, 20));
            Assert.Equal(0.5, Last(document).ConstantWidth, 9);
        }

        [Fact]
        public void ARectangleWithNoMode_IsAsPlainAsEverAndWritesNoModesLine()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(0.1, 0.1), P(0.3, 0.7));

            DxfPolyline rectangle = Last(document);
            Assert.Equal(4, rectangle.NumberOfVertices);
            Assert.Equal(0.0, rectangle.Elevation);
            Assert.Equal(0.0, rectangle.ConstantWidth);
            Assert.DoesNotContain(session.History, line =>
                line.StartsWith("Current rectangle modes", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void Dimensions_GiveTheSides_AndTheOtherCornerOnlyTheQuadrant()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), "D");
            Assert.Equal("Specify length for rectangles <10.0000>:", session.PromptText);
            Run(session, "30");
            Assert.Equal("Specify width for rectangles <10.0000>:", session.PromptText);
            Run(session, "20");
            Assert.Equal("Specify other corner point or [Area/Dimensions/Rotation]:", session.PromptText);
            Run(session, P(-30, -30));

            Assert.Equal("10,10 -20,10 -20,-10 10,-10", Vertices(Last(document)));

            // The sides are offered next time, and the corner picked up and to the right.
            Run(session, "RECTANG", P(0, 0), "D");
            Assert.Equal("Specify length for rectangles <30.0000>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify width for rectangles <20.0000>:", session.PromptText);
            Run(session, Enter, P(1, 1));
            Assert.Equal("0,0 30,0 30,20 0,20", Vertices(Last(document)));
        }

        [Fact]
        public void Area_AsksTheAreaThenTheSideItIsWorkedFrom_AndDrawsAtOnce()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), "A");
            Assert.Equal("Enter area of rectangle in current units <100.0000>:", session.PromptText);
            Run(session, "200");
            Assert.Equal("Calculate rectangle dimensions based on [Length/Width] <Length>:", session.PromptText);
            Run(session, "L");
            Assert.Equal("Enter rectangle length <10.0000>:", session.PromptText);
            Run(session, "20");

            Assert.False(session.IsRunning);
            Assert.Equal("10,10 30,10 30,20 10,20", Vertices(Last(document)));

            // By width: 200 over a width of 5 is a length of 40; the area is offered again, and the width the first gave.
            Run(session, "RECTANG", P(10, 10), "A");
            Assert.Equal("Enter area of rectangle in current units <200.0000>:", session.PromptText);
            Run(session, Enter, "W");
            Assert.Equal("Enter rectangle width <10.0000>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("10,10 50,10 50,15 10,15", Vertices(Last(document)));
        }

        [Fact]
        public void Area_TakesTheCornersOffTheArea_OfAChamferAndOfAFillet()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // A chamfer of 2 and 3 takes two triangles of 3 off: 20 by 10.6 less 12 is 200.
            Run(session, "RECTANG", "C", "2", "3", P(10, 10), "A", "200", "L", "20");
            Assert.Equal("10,12 13,10 28,10 30,13 30,18.6 27,20.6 12,20.6 10,17.6", Vertices(Last(document)));
            DxfPolyline chamfered = Last(document);
            Assert.Equal(8, chamfered.NumberOfVertices);
            Assert.Equal(10.0 + 10.6, chamfered.GetPoint2dAt(5).Y, 9);

            // A fillet of 2 takes the square on 2 less its quarter circle, four times: 20 by 10.1717 less 3.4336 is 200.
            Run(session, "RECTANG", "F", "2", P(10, 10), "A", "200", "L", "20");
            DxfPolyline rounded = Last(document);
            double height = (200.0 + (4.0 - Math.PI) * 4.0) / 20.0;
            Assert.Equal(10.0 + height, rounded.GetPoint2dAt(5).Y, 9);
        }

        [Fact]
        public void AnAreaThatIsNotPositive_IsRefusedAndAskedAgain()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "RECTANG", P(10, 10), "A", "0");

            Assert.Contains("Value must be positive and nonzero.", session.History);
            Assert.Equal("Enter area of rectangle in current units <100.0000>:", session.PromptText);
        }

        [Fact]
        public void ARotation_TurnsTheSidesAboutTheFirstCorner_AndStaysForTheNextRectangle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), "R");
            Assert.Equal("Specify rotation angle or [Pick points] <0>:", session.PromptText);
            Run(session, "30");
            Assert.Equal("Specify other corner point or [Area/Dimensions/Rotation]:", session.PromptText);
            Run(session, P(40, 40));

            DxfPolyline turned = Last(document);
            Assert.Equal("10,10 45.4904,30.4904 40,40 4.5096,19.5096", Vertices(turned));

            // The rotation is a mode: listed, drawn with the next rectangle, and offered at its own prompt.
            Run(session, "RECTANG");
            Assert.Contains("Current rectangle modes:  Rotation=30", session.History);
            Run(session, P(10, 10), P(40, 40));
            Assert.Equal("10,10 45.4904,30.4904 40,40 4.5096,19.5096", Vertices(Last(document)));
            Run(session, "RECTANG", P(10, 10), "R");
            Assert.Equal("Specify rotation angle or [Pick points] <30>:", session.PromptText);
            Run(session, "0", P(40, 40));
            Assert.Equal("10,10 40,10 40,40 10,40", Vertices(Last(document)));
        }

        [Fact]
        public void RotationByPickPoints_TakesTheDirectionOfTheTwoPoints_AndDimensionsFollowIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), "R", "P");
            Assert.Equal("Specify first point:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, P(10, 10), "D", "20", "10", P(0, 50));

            // 20 along the 45 degree direction, 10 across it, to the side of the turned axes the other corner is on.
            double s = Math.Sqrt(0.5);
            DxfPolyline rectangle = Last(document);
            Near(P(10, 10), rectangle.GetPoint2dAt(0));
            Near(P(10 + 20 * s, 10 + 20 * s), rectangle.GetPoint2dAt(1), 1E-9);
            Near(P(10 + 20 * s - 10 * s, 10 + 20 * s + 10 * s), rectangle.GetPoint2dAt(2), 1E-9);
        }

        [Fact]
        public void ARotationAskedAfterDimensions_GivesTheSidesUp()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), "D", "20", "10", "R", "0", P(50, 40));

            Assert.Equal("10,10 50,10 50,40 10,40", Vertices(Last(document)));
        }

        [Fact]
        public void ARectangleFromTwoCornersInALine_IsRefusedAsBefore()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", P(10, 10), P(10, 30));

            Assert.Contains("*Invalid* The corners lie in a line.", session.History);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void ARectangleOptionsDrawn_IsOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "F", "2", P(10, 10), P(40, 30));
            Assert.Single(All<DxfPolyline>(document.ModelSpace));

            Assert.True(document.Undo());
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void TheRectangleOfOptions_ReopensFromItsText()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "RECTANG", "F", "2", "E", "3", "W", "0.5", P(10, 10), P(40, 30));

            AssertReopens(document);
        }
    }
}
