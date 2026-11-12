using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Radii and diameters whose words are put within the room of an arrowhead of the rim, and DIM's radial and chained
    /// options, as AutoCAD 2021 draws and asks them. The drawings are blank ones with acad.dwt's Standard, its text style
    /// Arial, as accoreconsole made them: arrowheads and words 0.18, DIMGAP 0.09, "R20.0000" 1.0588 wide.
    /// </summary>
    public class DimensionFollowUps5Tests
    {
        /// <summary>A blank drawing whose Standard is current, its words aligned and above the line, DIMTOFL on.</summary>
        private static CadCommandSession Aligned()
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(document.Database);
            document.Database.Dimtad = 1;
            document.Database.Dimtofl = true;
            document.Database.Dimtih = false;
            document.Database.Dimtoh = false;
            return Session(document);
        }

        /// <summary>A circle about the origin, then a radius or a diameter of it with its words put at a point.</summary>
        private static DxfDimension Round(CadCommandSession session, string command, double radius, GeoPoint2 at)
        {
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, command, new[] { circle }, at);
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static void Near(double x, double y, GeoPoint3 actual, double tolerance)
        {
            Assert.True(
                Math.Abs(actual.X - x) < tolerance && Math.Abs(actual.Y - y) < tolerance,
                $"expected ({x}, {y}), got ({actual.X}, {actual.Y})"
            );
        }

        private static void HasLine(DxfDimension dimension, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1E-3;
            Assert.Contains(dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased), l =>
                (
                    Math.Abs(l.StartPoint.X - x1) < tolerance
                    && Math.Abs(l.StartPoint.Y - y1) < tolerance
                    && Math.Abs(l.EndPoint.X - x2) < tolerance
                    && Math.Abs(l.EndPoint.Y - y2) < tolerance
                )
                || (
                    Math.Abs(l.StartPoint.X - x2) < tolerance
                    && Math.Abs(l.StartPoint.Y - y2) < tolerance
                    && Math.Abs(l.EndPoint.X - x1) < tolerance
                    && Math.Abs(l.EndPoint.Y - y1) < tolerance
                )
            );
        }

        [Theory]
        [InlineData(20.2)]
        [InlineData(20.5)]
        [InlineData(20.9)]
        public void ARadiusWithItsWordsPutJustPastTheRim_StandsWhereTheFitPutsThem(double distance)
        {
            // AutoCAD 2021 (probe GhDm5): "R20.0000" put 20.2, 20.5 or 20.9 out at 30 degrees, aligned above its line, stands
            // 20.979 out (the arrowhead 0.18 twice, DIMGAP and half the words' width past the rim), middle (18.0777, 10.6472),
            // the line from the back of the arrowhead (17.4764, 10.09) to (18.7051, 10.7994).
            CadCommandSession session = Aligned();
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, distance));

            Near(18.0777, 10.6472, Words(radius).Location, 1E-3);
            HasLine(radius, 17.4764, 10.09, 18.7051, 10.7994);
            HasLine(radius, 0, 0, 17.3205, 10);
        }

        [Fact]
        public void ARadiusWithItsWordsPutBeyondThatRoom_StandsAtThePoint()
        {
            // AutoCAD 2021 (probe GhDm5): put 21 out at 30 degrees, the same words stand at (18.0956, 10.6575), the lift of
            // the line they stand by taken off the point (18.1865, 10.5).
            CadCommandSession session = Aligned();
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 21));

            Near(18.0956, 10.6575, Words(radius).Location, 1E-3);
            HasLine(radius, 17.4764, 10.09, 18.7229, 10.8097);
        }

        [Fact]
        public void ADiameterWithItsWordsPutJustPastTheRim_StandsWhereTheFitPutsThem()
        {
            // AutoCAD 2021 (probe GhDm5): "%%c40.0000" put 20.3 out at 30 degrees stands (18.0854, 10.6575), a line from
            // (17.4764, 10.09) to (18.7255, 10.8112), and the far arrowhead outside the rim on its short line.
            CadCommandSession session = Aligned();
            DxfDimension diameter = Round(session, "DDI", 20, Polar(30, 20.3));

            Near(18.0854, 10.6575, Words(diameter).Location, 1E-3);
            HasLine(diameter, 17.4764, 10.09, 18.7255, 10.8112);
            HasLine(diameter, -17.4764, -10.09, -17.6323, -10.18);
            HasLine(diameter, -17.3205, -10, 17.3205, 10);
        }

        /// <summary>A blank drawing whose Standard is current, its words level (DIMTOH, DIMTIH) and on the line (DIMTAD 0).</summary>
        private static CadCommandSession Level()
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(document.Database);
            document.Database.Dimtad = 0;
            document.Database.Dimtoh = true;
            document.Database.Dimtih = true;
            return Session(document);
        }

        [Theory]
        [InlineData("DAL", -0.2, 10, 9.3567, 9.82)]
        [InlineData("DAL", -0.2, -10, -10.6433, -10.18)]
        [InlineData("DAL", 0.2, 10, 10.6433, 10.18)]
        [InlineData("DAL", 0.2, -10, -9.3567, -9.82)]
        [InlineData("DLI", -0.2, 10, 9.3567, 9.82)]
        [InlineData("DLI", -0.2, -10, -10.6433, -10.18)]
        public void LevelWordsOutsideAnUprightLine_GoLeftWhereItsPointsRunDown_AndRightWhereTheyRunUp(
            string command,
            double secondY,
            double lineX,
            double wordsX,
            double landingEndX
        )
        {
            // AutoCAD 2021 (probe GhDm5): a dimension of 0.2 along x = 0, too short for "0.2000", its line put 10 to the right
            // or the left, has its words past the end of the line, level, on a landing 0.18 long that runs left from the
            // line (x 9.82 from 10) where the points run down, and right where they run up, whichever side the line is on:
            // "0.2000" stands (9.3567, -0.56) with the points running down and the line at x = 10, (10.6433, 0.56) running up.
            CadCommandSession session = Level();
            double sign = secondY < 0 ? -1.0 : 1.0;
            Run(session, command, P(0, 0), P(0, secondY), P(lineX, secondY / 2));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Single();

            Near(wordsX, sign * 0.56, Words(dimension).Location, 1E-3);
            HasLine(dimension, lineX, sign * 0.56, landingEndX, sign * 0.56);
        }

        [Theory]
        [InlineData("DRA", 0, -25, -0.7994, -0.18)]
        [InlineData("DRA", 0, 25, 0.7994, 0.18)]
        [InlineData("DDI", 0, -25, -0.8112, -0.18)]
        public void LevelWordsOutsideAnUprightRadial_LandTheWayItRunsOut(
            string command,
            double lineX,
            double lineY,
            double wordsX,
            double landingEndX
        )
        {
            // AutoCAD 2021 (probe GhDm5): "R20.0000" of a circle of 20 about the origin put 25 out, straight down or up,
            // stands on a landing 0.18 long that runs left where the line runs down and right where it runs up: down it is
            // (-0.18, -25) to (0, -25), the words' middle (-0.7994, -25); a diameter's "%%c40.0000" (-0.8112, -25).
            CadCommandSession session = Level();
            DxfDimension dimension = Round(session, command, 20, P(lineX, lineY));

            Near(wordsX, lineY, Words(dimension).Location, 1E-3);
            HasLine(dimension, landingEndX, lineY, 0, lineY);
        }

        [Theory]
        [InlineData(25, 20.18, 24.3806)]
        [InlineData(-25, -20.18, -24.3806)]
        public void LevelWordsOutsideALevelRadial_StandAlongItWithNoLanding(double x, double from, double to)
        {
            // AutoCAD 2021 (probe GhDm5): on a line exactly level the words that DIMTOH keeps level are the words that stand
            // along it: "R20.0000" put 25 out to the right has its middle at (25, 0) and its line from the back of the
            // arrowhead (20.18, 0) to a gap short of them (24.3806, 0), no landing past a bend.
            CadCommandSession session = Level();
            DxfDimension radius = Round(session, "DRA", 20, P(x, 0));

            Near(x, 0, Words(radius).Location, 1E-3);
            HasLine(radius, from, 0, to, 0);
            Assert.All(radius.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased), l =>
                Assert.True(
                    Math.Abs(l.StartPoint.X) < Math.Abs(to) + 1E-3 && Math.Abs(l.EndPoint.X) < Math.Abs(to) + 1E-3
                )
            );
        }

        [Theory]
        [InlineData(19.5, -0.7994, -0.18)]
        [InlineData(-19.5, 0.7994, 0.18)]
        public void LevelWordsInsideAnUprightRadial_LandTheWayItRunsIn(double y, double wordsX, double landingEndX)
        {
            // AutoCAD 2021 (probe GhDm5): put 19.5 from the centre straight up, "R20.0000" kept level by DIMTIH stands on a
            // landing that runs left, (399.82 to 400 of a circle at 400), the middle 0.7994 left of the line; put 19.5 straight
            // down, 0.7994 right of it.
            CadCommandSession session = Level();
            DxfDimension radius = Round(session, "DRA", 20, P(0, y));

            Near(wordsX, y, Words(radius).Location, 1E-3);
            HasLine(radius, landingEndX, y, 0, y);
        }

        [Fact]
        public void Dim_ContinueAsksForTheExtensionLineOriginToContinueFrom_AsAutoCadsDimDoes()
        {
            // AutoCAD 2021's DIM (its own resource strings): Continue asks "Select extension line origin to continue or
            // [Baseline]:" where DIMCONTINUE goes on from the last dimension, then "Specify second extension line origin or
            // [Select/Undo] <Select>:"; the row goes on from the extension line picked, the second one of the dimension of 100.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            Run(session, "DIM", "C");
            Assert.Equal("Select extension line origin to continue or [Baseline]:", session.PromptText);
            Run(session, P(100, -15));
            Assert.Equal("Specify second extension line origin or [Select/Undo] <Select>:", session.PromptText);
            Run(session, P(250, 0));

            List<DxfRotatedDimension> row = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(new[] { 100.0, 150.0 }, row.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(100.0, row[1].XLine1Point.X, 9);

            Run(session, Enter);
            Assert.Equal("Select extension line origin to continue or [Baseline]:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith(
                "Select objects or specify first extension line origin",
                session.PromptText,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void Dim_BaselineGoesOnFromTheFirstExtensionLinePicked_ItsOffsetSpacesTheRow_AndContinueTurnsItIntoOne()
        {
            // AutoCAD 2021's DIM: Baseline asks "Select extension line origin as baseline or [Continue]:", then "Specify
            // second extension line origin or [Select/Offset/Undo] <Select>:"; Offset gives the spacing of the row, 20 here
            // where DIMDLI says 0.38, and Continue goes over to "Select extension line origin to continue or [Baseline]:".
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            Run(session, "DIM", "B");
            Assert.Equal("Select extension line origin as baseline or [Continue]:", session.PromptText);
            Run(session, "C");
            Assert.Equal("Select extension line origin to continue or [Baseline]:", session.PromptText);
            Run(session, "B", P(0, -15));
            Assert.Equal("Specify second extension line origin or [Select/Offset/Undo] <Select>:", session.PromptText);
            Run(session, "O");
            Assert.Equal("Specify offset distance:", session.PromptText);
            Run(session, "20", P(300, 0));

            List<DxfRotatedDimension> row = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(new[] { 100.0, 300.0 }, row.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(-50.0, row[1].DimLinePoint.Y, 9);
        }

        [Fact]
        public void Dim_ACirclePickedOffersNoAngle_AnArcDoes()
        {
            // AutoCAD 2021's DIM: "Specify diameter dimension location or [Radius/Mtext/Text/text aNgle/Undo]:" for a circle,
            // "Specify radius dimension location or [Diameter/Angular/Mtext/Text/text aNgle/Undo]:" for an arc.
            CadCommandSession session = Aligned();
            Run(session, "C", P(0, 0), "20", "A", P(100, 0), P(120, 20), P(140, 0));

            Run(session, "DIM", P(20, 0));
            Assert.Equal(
                "Specify diameter dimension location or [Radius/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
            Run(session, Esc);

            Run(session, "DIM", P(120, 20));
            Assert.Equal(
                "Specify radius dimension location or [Diameter/Angular/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
        }

        [Fact]
        public void ARadiusWithItsWordsPutOnTheRim_IsOutsideItAndOneAMillionthIn_IsInside()
        {
            // AutoCAD 2021 (probe GhDm5): in a circle of 16 the point on the rim puts the words past it, 16.9794 out, and a
            // point 1E-7 inside keeps them inside, 15.0206 from the centre.
            CadCommandSession session = Aligned();
            DxfDimension onRim = Round(session, "DRA", 16, P(16, 0));
            Near(16.9794, 0.1819, Words(onRim).Location, 1E-3);

            session = Aligned();
            DxfDimension inside = Round(session, "DRA", 16, P(16 - 1E-7, 0));
            Near(15.0206, 0.1819, Words(inside).Location, 1E-3);
        }
    }
}
