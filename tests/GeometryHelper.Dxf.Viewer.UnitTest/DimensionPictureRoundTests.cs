using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The pictures of radius, diameter and angular dimensions, of arrowheads other than closed filled, of DIMTVP and of a
    /// DIMGAP below nought, as AutoCAD 2021 draws them. The numbers are AutoCAD's, read from the blocks it drew (with ISO-25's
    /// sizes: arrowheads and words 2.5, DIMGAP 0.625), but where they hang on how wide the words are: AutoCAD measures them by
    /// the font, the picture 0.7 of their height a letter, so there the test works from that width.
    /// </summary>
    public class DimensionPictureRoundTests
    {
        private const double Gap = 0.625;
        private const double Height = 2.5;
        private const double Arrow = 2.5;
        private const double Lift = Gap + Height / 2.0;
        private const double Letter = 0.7 * Height;

        private static readonly GeoVector2 Out30 = new GeoVector2(Math.Cos(Math.PI / 6.0), Math.Sin(Math.PI / 6.0));
        private static readonly GeoVector2 Up30 = new GeoVector2(-Math.Sin(Math.PI / 6.0), Math.Cos(Math.PI / 6.0));

        /// <summary>A blank drawing whose Standard style has ISO-25's sizes, whole numbers, and the variables set as given.</summary>
        private static CadCommandSession Metric(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimscale = 1.0;
            style.Dimasz = Arrow;
            style.Dimtxt = Height;
            style.Dimgap = Gap;
            style.Dimexo = 0.625;
            style.Dimexe = 1.25;
            style.Dimdec = 0;
            style.Dimadec = 0;
            style.Dimtad = 1;
            style.Dimtih = false;
            style.Dimtoh = false;
            style.Dimtofl = true;
            style.Dimatfit = 3;
            style.Dimcen = 2.5;
            style.Dimjust = 0;
            style.Dimtvp = 0.0;
            set(style);
            return Session(document);
        }

        private static DxfDimension Made(CadCommandSession session) => session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) => dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static List<DxfArc> Arcs(DxfDimension dimension) => dimension.DimBlock.OfType<DxfArc>().Where(a => !a.IsErased).ToList();

        private static List<DxfSolid> Solids(DxfDimension dimension) => dimension.DimBlock.OfType<DxfSolid>().Where(s => !s.IsErased).ToList();

        private static GeoPoint2 P2(GeoPoint3 p) => new GeoPoint2(p.X, p.Y);

        /// <summary>A point at a distance along the way at 30 degrees from the origin, and across it to the left.</summary>
        private static GeoPoint2 At30(double along, double across = 0.0) => new GeoPoint2(0, 0) + Out30 * along + Up30 * across;

        private static GeoPoint2 Polar(double degrees, double radius) => new GeoPoint2(radius * Math.Cos(degrees * Math.PI / 180.0), radius * Math.Sin(degrees * Math.PI / 180.0));

        private static bool Same(GeoPoint2 a, GeoPoint2 b, double tolerance) => (a - b).Length <= tolerance;

        private static void HasLine(DxfDimension dimension, GeoPoint2 a, GeoPoint2 b, double tolerance = 1E-3)
            => Assert.Contains(Lines(dimension), l => (Same(P2(l.StartPoint), a, tolerance) && Same(P2(l.EndPoint), b, tolerance)) || (Same(P2(l.StartPoint), b, tolerance) && Same(P2(l.EndPoint), a, tolerance)));

        private static void HasArc(DxfDimension dimension, double radius, double startDegrees, double endDegrees, double tolerance = 2E-3)
            => Assert.Contains(Arcs(dimension), a => Math.Abs(a.Radius - radius) < 1E-9 && Math.Abs(Math.IEEERemainder(a.StartAngle - startDegrees * Math.PI / 180.0, 2 * Math.PI)) < tolerance && Math.Abs(Math.IEEERemainder(a.EndAngle - endDegrees * Math.PI / 180.0, 2 * Math.PI)) < tolerance);

        /// <summary>The middle of the back of an arrowhead's SOLID, the two corners that are not its tip.</summary>
        private static GeoPoint2 Back(DxfSolid solid)
        {
            GeoPoint2 a = P2(solid.GetPointAt(1));
            GeoPoint2 b = P2(solid.GetPointAt(2));
            return new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        }

        private static DxfSolid ArrowAt(DxfDimension dimension, GeoPoint2 tip) => Assert.Single(Solids(dimension), s => Same(P2(s.GetPointAt(0)), tip, 1E-3));

        private static string Circle(CadCommandSession session, double radius)
        {
            Run(session, "C", P(0, 0), radius.ToString(CultureInfo.InvariantCulture));
            return session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
        }

        [Fact]
        public void ARadiusPutOutside_HasItsArrowheadOutsideTheRim_ItsLineFromTheBackOfIt_AndTheLineInsideByDimtofl()
        {
            CadCommandSession session = Metric(s => { });
            string circle = Circle(session, 20);

            Run(session, "DRA", new[] { circle }, At30(45));

            DxfDimension dimension = Made(session);
            DxfSolid arrow = ArrowAt(dimension, At30(20));
            Near(At30(22.5), Back(arrow), 1E-3);

            // AutoCAD 2021: the line from (19.4856, 11.25) on past the words "R20"; the line from the centre to the rim.
            List<DxfLine> lines = Lines(dimension);
            Assert.Equal(2, lines.Count);
            HasLine(dimension, new GeoPoint2(19.4856, 11.25), At30(45 + 3 * Letter / 2 + Gap));
            HasLine(dimension, new GeoPoint2(0, 0), new GeoPoint2(17.3205, 10));
            Near(At30(45, Lift), P2(Words(dimension).Location), 1E-9);
            Assert.Equal(Math.PI / 6.0, Words(dimension).Rotation, 9);
        }

        [Fact]
        public void WithDimtoflOff_ARadiusOutsideHasTheCentreMarkOfDimcen_AndCentreLinesWhereItIsBelowNought()
        {
            CadCommandSession session = Metric(s => s.Dimtofl = false);
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));

            DxfDimension dimension = Made(session);
            Assert.Equal(3, Lines(dimension).Count);
            HasLine(dimension, new GeoPoint2(-2.5, 0), new GeoPoint2(2.5, 0));
            HasLine(dimension, new GeoPoint2(0, -2.5), new GeoPoint2(0, 2.5));

            session = Metric(s => { s.Dimtofl = false; s.Dimcen = -2.5; });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));
            dimension = Made(session);
            Assert.Equal(7, Lines(dimension).Count);
            HasLine(dimension, new GeoPoint2(-5, 0), new GeoPoint2(-22.5, 0));
            HasLine(dimension, new GeoPoint2(5, 0), new GeoPoint2(22.5, 0));
            HasLine(dimension, new GeoPoint2(0, -5), new GeoPoint2(0, -22.5));
            HasLine(dimension, new GeoPoint2(0, 5), new GeoPoint2(0, 22.5));
        }

        [Fact]
        public void ARadiusPutInside_HasItsArrowheadInsideTheRim_AndItsLineFromTheBackOfItInPastTheWords()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(8));

            DxfDimension dimension = Made(session);
            Near(At30(17.5), Back(ArrowAt(dimension, At30(20))), 1E-3);
            Assert.Single(Lines(dimension));
            HasLine(dimension, new GeoPoint2(15.1554, 8.75), At30(8 - 3 * Letter / 2 - Gap));
            Near(At30(8, Lift), P2(Words(dimension).Location), 1E-9);

            // DIMTAD 0: on the line, which stops short of them on the side of the rim.
            session = Metric(s => s.Dimtad = 0);
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(8));
            dimension = Made(session);
            HasLine(dimension, new GeoPoint2(15.1554, 8.75), At30(8 + 3 * Letter / 2 + Gap));
            Near(At30(8), P2(Words(dimension).Location), 1E-9);
        }

        [Fact]
        public void Dimtoh_KeepsTheWordsOfARadiusOutsideLevel_OnALandingFromTheEndOfItsLine()
        {
            CadCommandSession session = Metric(s => { s.Dimtad = 0; s.Dimtoh = true; });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));

            DxfDimension dimension = Made(session);
            HasLine(dimension, new GeoPoint2(19.4856, 11.25), new GeoPoint2(38.9711, 22.5));
            HasLine(dimension, new GeoPoint2(38.9711, 22.5), new GeoPoint2(41.4711, 22.5));
            Assert.Equal(0.0, Words(dimension).Rotation, 12);
            Near(new GeoPoint2(41.4711 + Gap + 3 * Letter / 2, 22.5), P2(Words(dimension).Location), 1E-3);

            // DIMTAD 1: above a landing as long as the words and the gap.
            session = Metric(s => { s.Dimtad = 1; s.Dimtoh = true; });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));
            dimension = Made(session);
            HasLine(dimension, new GeoPoint2(38.9711, 22.5), new GeoPoint2(38.9711 + 3 * Letter + Gap, 22.5));
            Near(new GeoPoint2(38.9711 + Gap + 3 * Letter / 2, 22.5 + Lift), P2(Words(dimension).Location), 1E-3);
        }

        [Fact]
        public void ARadiusTooSmallForItsWords_Dimatfit0MovesThemAndTheArrowheadOut_AndDimatfit3KeepsThemInsideClearOfTheRim()
        {
            CadCommandSession session = Metric(s => s.Dimatfit = 0);
            Run(session, "DRA", new[] { Circle(session, 3) }, At30(1));

            DxfDimension dimension = Made(session);
            Near(At30(5.5), Back(ArrowAt(dimension, At30(3))), 1E-3);
            HasLine(dimension, new GeoPoint2(0, 0), At30(3));
            double middle = 3 + 2 * Arrow + Gap + Letter;
            HasLine(dimension, At30(5.5), At30(middle + Letter + Gap));
            Near(At30(middle, Lift), P2(Words(dimension).Location), 1E-9);

            // DIMATFIT 3: inside, two arrowheads and the gap in from the rim, the arrowhead inside.
            session = Metric(s => s.Dimatfit = 3);
            Run(session, "DRA", new[] { Circle(session, 3) }, At30(1));
            dimension = Made(session);
            Near(At30(0.5), Back(ArrowAt(dimension, At30(3))), 1E-3);
            middle = 3 - 2 * Arrow - Gap - Letter;
            Near(At30(middle, Lift), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, At30(0.5), At30(middle - Letter - Gap));
        }

        [Fact]
        public void Dimatfit0To2_PutTheWordsInsideARadiusInTheMiddleOfItsLine_WhereTheyFit()
        {
            CadCommandSession session = Metric(s => s.Dimatfit = 1);
            Run(session, "DRA", new[] { Circle(session, 12) }, At30(4));

            // AutoCAD 2021: the words at 4.75, half way from the centre to the back of the arrowhead, and the line between.
            DxfDimension dimension = Made(session);
            Near(At30(4.75, Lift), P2(Words(dimension).Location), 1E-9);
            Assert.Single(Lines(dimension));
            HasLine(dimension, new GeoPoint2(0, 0), At30(9.5));
        }

        [Fact]
        public void ADiameterPutOutside_HasBothArrowheadsOutside_TheFarOneOnItsShortLine_AndTheLineThroughTheCircle()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DDI", new[] { Circle(session, 20) }, At30(45));

            DxfDimension dimension = Made(session);
            Near(At30(22.5), Back(ArrowAt(dimension, At30(20))), 1E-3);
            Near(At30(-22.5), Back(ArrowAt(dimension, At30(-20))), 1E-3);
            HasLine(dimension, new GeoPoint2(-19.4856, -11.25), new GeoPoint2(-21.6506, -12.5));
            HasLine(dimension, new GeoPoint2(-17.3205, -10), new GeoPoint2(17.3205, 10));
            Assert.Equal(3, Lines(dimension).Count);
        }

        [Fact]
        public void ADiameterThatTakesBoth_UnderDimatfit0_HasItsWordsAtTheCentreBetweenTwoArrowheadsInside()
        {
            CadCommandSession session = Metric(s => s.Dimatfit = 0);
            Run(session, "DDI", new[] { Circle(session, 4) }, At30(4.0 / 3.0));

            DxfDimension dimension = Made(session);
            Near(At30(1.5), Back(ArrowAt(dimension, At30(4))), 1E-3);
            Near(At30(-1.5), Back(ArrowAt(dimension, At30(-4))), 1E-3);
            Assert.Single(Lines(dimension));
            HasLine(dimension, At30(-1.5), At30(1.5));
            Near(At30(0, Lift), P2(Words(dimension).Location), 1E-9);
        }

        [Fact]
        public void AnAngle_HasItsArcFromTheBackOfOneArrowheadToTheBackOfTheOther_TheArrowheadsAlongTheirChords()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));

            DxfDimension dimension = Made(session);

            // AutoCAD 2021: the arc from 0.0834 to 0.9638 radians, each arrowhead's back on it 2.5 from its tip.
            DxfArc arc = Assert.Single(Arcs(dimension));
            double back = 2 * Math.Asin(Arrow / 60.0);
            Assert.Equal(back, arc.StartAngle, 9);
            Assert.Equal(Math.PI / 3 - back, arc.EndAngle, 9);
            Near(new GeoPoint2(29.8958, 2.4982), Back(ArrowAt(dimension, P(30, 0))), 1E-3);
            Near(Polar(30, 30 + Lift), P2(Words(dimension).Location), 1E-9);
            Assert.Equal(5 * Math.PI / 3, Words(dimension).Rotation, 9);
            HasLine(dimension, P(39.375, 0), P(28.75, 0));
        }

        [Fact]
        public void AnAngleWithDimtad0_HasItsArcBrokenAboutTheWordsOnIt()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 0);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));

            DxfDimension dimension = Made(session);
            double half = Math.Asin((3 * Letter / 2 + Gap) / 30.0) * 180 / Math.PI;
            Assert.Equal(2, Arcs(dimension).Count);
            HasArc(dimension, 30, 4.7757, 30 - half);
            HasArc(dimension, 30, 30 + half, 55.2243);
            Near(Polar(30, 30), P2(Words(dimension).Location), 1E-9);
        }

        [Fact]
        public void TheWordsOfAnAngle_StandOutsideTheArcAsTheyRead_InsideWhereTheyReadTheOtherWay_AndByDimtad2AwayFromThePoints()
        {
            // AutoCAD 2021 puts the words of 200 to 260 degrees at 28.1 from the vertex, of 0 to 60 under DIMTAD 2 at 28.1, with
            // the points 20 out and the arc 45 at 46.9, and under DIMTAD 4 at 28.1.
            CadCommandSession session = Metric(s => s.Dimtad = 1);
            Run(session, "DAN", Enter, P(0, 0), Polar(200, 40), Polar(260, 40), Polar(230, 30));
            Near(Polar(230, 30 - Lift), P2(Words(Made(session)).Location), 1E-9);

            session = Metric(s => s.Dimtad = 2);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));
            Near(Polar(30, 30 - Lift), P2(Words(Made(session)).Location), 1E-9);
            Run(session, "DAN", Enter, P(0, 0), P(20, 0), Polar(60, 20), Polar(30, 45));
            Near(Polar(30, 45 + Lift), P2(Words(Made(session)).Location), 1E-9);

            session = Metric(s => s.Dimtad = 4);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));
            Near(Polar(30, 30 - Lift), P2(Words(Made(session)).Location), 1E-9);
        }

        [Fact]
        public void ASmallAngle_HasItsWordsAndArrowheadsOutsidePastTheFirstRay_TheArcRunOnAndALineAlongItToTheWords()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(8, 40), Polar(4, 20));

            // AutoCAD 2021: the arrowheads' arcs 345.665 to 352.833 and 15.166 to 22.334 degrees, the arc run on from 345.637,
            // the arc 0 to 8 between (DIMTOFL), and the words outside past 0 degrees, turned to 255.64.
            DxfDimension dimension = Made(session);
            Assert.Equal(4, Arcs(dimension).Count);
            HasArc(dimension, 20, 345.665, 352.833);
            HasArc(dimension, 20, 15.166, 22.334);
            HasArc(dimension, 20, 345.637, 0);
            HasArc(dimension, 20, 0, 8);
            Near(Polar(352.833, 20), Back(ArrowAt(dimension, P(20, 0))), 1E-3);
            GeoPoint2 end = Polar(345.637, 20);
            var way = new GeoVector2(Math.Sin(345.637 * Math.PI / 180), -Math.Cos(345.637 * Math.PI / 180));
            var up = new GeoVector2(Math.Cos(345.637 * Math.PI / 180), Math.Sin(345.637 * Math.PI / 180));
            HasLine(dimension, end, end + way * (2 * Letter + 2 * Gap));
            Near(end + way * (Gap + Letter) + up * Lift, P2(Words(dimension).Location), 1E-3);
            Assert.Equal(255.637, Words(dimension).Rotation * 180 / Math.PI, 2);
        }

        [Fact]
        public void WhereAnAngleTakesOnlyTheWords_Dimatfit1PutsTheArrowheadsOut_And2TheWords_DimtixKeepsBothIn_AndDimsoxdLeavesTheArrowheadsOut()
        {
            CadCommandSession session = Metric(s => s.Dimatfit = 1);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(20, 40), Polar(10, 20));
            DxfDimension dimension = Made(session);
            Near(Polar(10, 20 + Lift), P2(Words(dimension).Location), 1E-9);
            HasArc(dimension, 20, 345.665, 352.833);
            HasArc(dimension, 20, 27.164, 34.332);
            HasArc(dimension, 20, 0, 20);

            session = Metric(s => s.Dimatfit = 2);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(20, 40), Polar(10, 20));
            dimension = Made(session);
            HasArc(dimension, 20, 7.168, 12.834);
            HasArc(dimension, 20, 352.833, 0);
            Assert.True(P2(Words(dimension).Location).Y < 0);

            // An angle of 8 degrees under DIMTIX: the arrowheads inside, the arc from the back of one to the back of the other.
            session = Metric(s => s.Dimtix = true);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(8, 40), Polar(4, 20));
            dimension = Made(session);
            HasArc(dimension, 20, 7.168, 0.831);
            Assert.Equal(2, Solids(dimension).Count);

            session = Metric(s => { s.Dimtix = true; s.Dimsoxd = true; });
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(8, 40), Polar(4, 20));
            dimension = Made(session);
            Assert.Empty(Arcs(dimension));
            Assert.Empty(Solids(dimension));
            Near(Polar(4, 20 + Lift), P2(Words(dimension).Location), 1E-9);
        }

        [Fact]
        public void Dimjust1PutsTheWordsOfAnAngleNextToItsSecondRay_And2NextToItsFirst()
        {
            CadCommandSession session = Metric(s => s.Dimjust = 1);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(90, 40), Polar(45, 30));
            double off = 2 * Math.Asin((Arrow + Gap + 3 * Letter / 2) / 60.0) * 180 / Math.PI;
            Near(Polar(90 - off, 30 + Lift), P2(Words(Made(session)).Location), 1E-9);

            session = Metric(s => s.Dimjust = 2);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(90, 40), Polar(45, 30));
            Near(Polar(off, 30 + Lift), P2(Words(Made(session)).Location), 1E-9);
        }

        [Fact]
        public void TheAngleOfTwoLinesBehindTheirVertex_HasItsExtensionLinesBackThroughIt_AndItsWordsInsideTheArc()
        {
            CadCommandSession session = Metric(s => { });
            Run(session, "L", P(0, 0), P(40, 0), Enter, "L", P(0, 0), Polar(45, 40), Enter);
            Run(session, "DAN", P(20, 0), Polar(45, 20), Polar(202, 25));

            DxfDimension dimension = Made(session);
            HasLine(dimension, P(-0.625, 0), P(-26.25, 0));
            HasLine(dimension, Polar(225, 0.625), Polar(225, 26.25));
            Near(Polar(202.5, 25 - Lift), P2(Words(dimension).Location), 1E-9);
        }

        [Fact]
        public void TheWordsOfAnAngleMovedByDimtedit_UnderDimtmove1StandLevelPastALeaderFromTheMiddleOfTheArc_AndUnder2TurnedAlongTheArc()
        {
            CadCommandSession session = Metric(s => s.Dimtmove = 1);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));
            DxfDimension dimension = Made(session);
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(30, 50));

            // AutoCAD 2021: a leader from the arc at 30 degrees to a landing an arrowhead long, the words level past it.
            dimension = Made(session);
            GeoPoint2 at = Polar(30, 50);
            GeoPoint2 knee = at - new GeoVector2(3 * Letter / 2 + Gap + Arrow, 0);
            HasLine(dimension, Polar(30, 30), knee);
            HasLine(dimension, knee, knee + new GeoVector2(Arrow, 0));
            Near(at, P2(Words(dimension).Location), 1E-9);
            Assert.Equal(0.0, Words(dimension).Rotation, 12);

            session = Metric(s => s.Dimtmove = 2);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));
            Run(session, "DIMTEDIT", new[] { Made(session).Handle }, Polar(80, 35));
            dimension = Made(session);
            Near(Polar(80, 35), P2(Words(dimension).Location), 1E-9);
            Assert.Equal(350, Words(dimension).Rotation * 180 / Math.PI, 6);
            HasArc(dimension, 30, 4.7757, 55.2243);
            Assert.Equal(2, Lines(dimension).Count);
        }

        [Theory]
        [InlineData("Closed filled", 2.5)]
        [InlineData("Closed blank", 2.5)]
        [InlineData("Closed", 2.5)]
        [InlineData("Dot", 2.5)]
        [InlineData("Open", 2.5)]
        [InlineData("Origin indicator", 2.5)]
        [InlineData("Right angle", 2.5)]
        [InlineData("Box filled", 2.5)]
        [InlineData("Datum triangle", 2.5)]
        [InlineData("Architectural tick", 0)]
        [InlineData("Oblique", 0)]
        [InlineData("Dot small", 0)]
        [InlineData("Dot small blank", 0)]
        [InlineData("Integral", 0)]
        [InlineData("None", 0)]
        public void TheDimensionLine_StartsAtTheBackOfEachArrowhead_ButRunsToTheTipOfThoseThatTakeNoRoom(string arrowhead, double inset)
        {
            CadCommandSession session = Metric(s => s.Dimtad = 1);
            DxfDatabase database = session.Document.Database;
            DxfDimStyleTableRecord style = database.DimStyleTable["Standard"];
            style.Dimblk = CadArrowheads.Ensure(database, arrowhead);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));

            DxfLine line = Assert.Single(Lines(Made(session)), l => Math.Abs(l.StartPoint.Y + 20) < 1E-9 && Math.Abs(l.EndPoint.Y + 20) < 1E-9 && Math.Abs(l.StartPoint.X - l.EndPoint.X) > 50);
            Assert.Equal(inset, Math.Min(line.StartPoint.X, line.EndPoint.X), 9);
            Assert.Equal(100 - inset, Math.Max(line.StartPoint.X, line.EndPoint.X), 9);
        }

        [Fact]
        public void TheTicksOfAnAngle_AreTurnedAlongTheArcAtTheirTips()
        {
            CadCommandSession session = Metric(s => { });
            DxfDatabase database = session.Document.Database;
            database.DimStyleTable["Standard"].Dimblk = CadArrowheads.Ensure(database, "Oblique");
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));

            // AutoCAD 2021: the tick at 30 on the first ray from (28.75, 1.25) to (31.25, -1.25), the arc there running down.
            HasLine(Made(session), P(28.75, 1.25), P(31.25, -1.25));
            HasLine(Made(session), new GeoPoint2(16.7075, 26.4383), new GeoPoint2(13.2925, 25.5232));
        }

        [Fact]
        public void AnArrowheadThatTakesNoRoom_StaysInsideAShortLine()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 1);
            DxfDatabase database = session.Document.Database;
            database.DimStyleTable["Standard"].Dimblk = CadArrowheads.Ensure(database, "Dot small");

            Run(session, "DLI", P(0, 0), P(4, 0), P(2, -20));

            // AutoCAD 2021 draws the line 0 to 4 and nothing past the extension lines.
            DxfLine line = Assert.Single(Lines(Made(session)), l => Math.Abs(l.StartPoint.Y + 20) < 1E-9 && Math.Abs(l.EndPoint.Y + 20) < 1E-9);
            Assert.Equal(0, Math.Min(line.StartPoint.X, line.EndPoint.X), 9);
            Assert.Equal(4, Math.Max(line.StartPoint.X, line.EndPoint.X), 9);
        }

        [Fact]
        public void Dimtvp_LiftsTheWordsOfDimtad0ByThatManyTimesTheirHeight_TheLineBrokenAboutThemBelow0Point7()
        {
            // AutoCAD 2021: DIMTVP 1, the words 2.553 above (their height by the font) and the line whole, 2.5 to 97.5.
            CadCommandSession session = Metric(s => { s.Dimtad = 0; s.Dimtvp = 1.0; });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            DxfDimension dimension = Made(session);
            Near(P(50, -20 + Height), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, P(2.5, -20), P(97.5, -20), 1E-9);

            session = Metric(s => { s.Dimtad = 0; s.Dimtvp = 0.5; });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            dimension = Made(session);
            Near(P(50, -20 + Height / 2), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, P(2.5, -20), P(50 - 3 * Letter / 2 - Gap, -20), 1E-9);

            session = Metric(s => { s.Dimtad = 0; s.Dimtvp = -1.0; });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            Near(P(50, -20 - Height), P2(Words(Made(session)).Location), 1E-9);

            // Upright: above as the words read, to the left.
            session = Metric(s => { s.Dimtad = 0; s.Dimtvp = 1.0; });
            Run(session, "DLI", P(0, 0), P(0, 100), P(-20, 50));
            Near(P(-20 - Height, 50), P2(Words(Made(session)).Location), 1E-9);
        }

        [Fact]
        public void ADimgapBelowNought_DrawsABoxRoundTheWords_AndLiftsWordsAboveTheLineByTheGapAgain()
        {
            CadCommandSession session = Metric(s => { s.Dimtad = 0; s.Dimgap = -Gap; });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            DxfDimension dimension = Made(session);
            double x = 3 * Letter / 2 + Gap;
            double y = Height / 2 + Gap;

            // AutoCAD 2021: four lines from the lower left about the words, the gap clear of them; the line broken about them.
            HasLine(dimension, P(50 - x, -20 - y), P(50 + x, -20 - y), 1E-9);
            HasLine(dimension, P(50 + x, -20 - y), P(50 + x, -20 + y), 1E-9);
            HasLine(dimension, P(50 + x, -20 + y), P(50 - x, -20 + y), 1E-9);
            HasLine(dimension, P(50 - x, -20 + y), P(50 - x, -20 - y), 1E-9);
            HasLine(dimension, P(2.5, -20), P(50 - x, -20), 1E-9);

            session = Metric(s => { s.Dimtad = 1; s.Dimgap = -Gap; });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            dimension = Made(session);
            Near(P(50, -20 + 2 * Gap + Height / 2), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, P(50 - x, -20 + Gap), P(50 + x, -20 + Gap), 1E-9);

            // A radius's words in their box, turned with them.
            session = Metric(s => { s.Dimtad = 1; s.Dimgap = -Gap; });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));
            dimension = Made(session);
            Near(At30(45, 2 * Gap + Height / 2), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, At30(45 - x, Gap), At30(45 + x, Gap), 1E-9);
        }

        /// <summary><see cref="Metric"/> with its words in arial.ttf, as acadiso.dwt has them, the dimension style's DIMTXSTY Standard.</summary>
        private static CadCommandSession Arial(Action<DxfDimStyleTableRecord> set)
        {
            CadCommandSession session = Metric(set);
            DxfTextStyleTableRecord standard = session.Document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            session.Document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            return session;
        }

        [Fact]
        public void TheWordsOfAnArialStyle_AreMeasuredByTheFont_AsAutoCadLaysThePictureOut()
        {
            CadCommandSession session = Arial(s => { });
            Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));
            DxfDimension dimension = Made(session);

            // AutoCAD 2021: "R20" 5.965 wide and 2.553 high, the words 0.625 + 1.276 off the line, the line run 2.983 + 0.625 past them.
            Near(new GeoPoint2(38.0204, 24.1467), P2(Words(dimension).Location), 1E-3);
            HasLine(dimension, new GeoPoint2(42.0954, 24.3038), new GeoPoint2(19.4856, 11.25));
            Assert.Equal(Height, Words(dimension).TextHeight, 9);
        }

        [Fact]
        public void ARadiusOf6InArial_TakesItsWords4Point03WideInside_ARadiusOf5PutsThemOut()
        {
            // AutoCAD 2021, DIMATFIT 0: "R6" in the middle of the line from the centre to the back of the arrowhead.
            CadCommandSession session = Arial(s => s.Dimatfit = 0);
            Run(session, "DRA", new[] { Circle(session, 6) }, At30(2));
            DxfDimension dimension = Made(session);
            Near(new GeoPoint2(0.5648, 2.5217), P2(Words(dimension).Location), 1E-3);
            HasLine(dimension, P(0, 0), new GeoPoint2(3.0311, 1.75));
            Assert.Single(Lines(dimension));
            ArrowAt(dimension, At30(6));

            session = Arial(s => s.Dimatfit = 0);
            Run(session, "DRA", new[] { Circle(session, 5) }, At30(5.0 / 3.0));
            Assert.True(Words(Made(session)).Location.DistanceTo(new GeoPoint3(0, 0, 0)) > 5.0);
        }

        [Fact]
        public void AnExtensionLineOfNoLength_IsDrawnFromDimexoToDimexePastItsPoint()
        {
            // AutoCAD 2021: the second point on the dimension line has its extension line from 0.625 to 1.25 up past it.
            CadCommandSession session = Metric(s => { });
            Run(session, "DLI", P(0, 0), P(60, 40), P(30, 40));
            HasLine(Made(session), P(60, 40.625), P(60, 41.25));
        }

        [Fact]
        public void DimteditOfAnAngleUnderDimtmove0_RunsTheArcThroughThePoint_TheWordsAboveItAsTheyRead()
        {
            CadCommandSession session = Arial(s => s.Dimtmove = 0);
            Run(session, "DAN", Enter, P(0, 0), P(40, 0), Polar(60, 40), Polar(30, 30));
            DxfDimension dimension = Made(session);
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(30, 50));

            // AutoCAD 2021: the arc 50 from the vertex, the words "60°" 1.902 further out, the text's point the one given.
            HasArc(dimension, 50, 2.865, 57.135);
            Near(new GeoPoint2(44.948, 25.9507), P2(Words(dimension).Location), 1E-3);
            Near(Polar(30, 50), P2(dimension.TextPosition), 1E-9);

            // Past the second ray, at 80 degrees: the arc 35 out, the words on it there, turned along it.
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(80, 35));
            HasArc(dimension, 35, 4.091, 55.909);
            Near(new GeoPoint2(6.4079, 36.3408), P2(Words(dimension).Location), 1E-3);
            Assert.Equal(6.1087, Words(dimension).Rotation, 3);
        }

        [Fact]
        public void DimteditOfARadiusUnderDimtmove0And1_SlidesTheWordsAlongItsLine_Under2PutsThemAtThePoint()
        {
            foreach (int move in new[] { 0, 1 })
            {
                CadCommandSession session = Arial(s => s.Dimtmove = move);
                Run(session, "DRA", new[] { Circle(session, 20) }, At30(45));
                DxfDimension dimension = Made(session);
                Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(60, 50));

                // AutoCAD 2021: the line stays at 30 degrees; the words 43.3 out along it, where the point falls on it.
                var radial = (DxfRadialDimension)dimension;
                Near(At30(20), P2(radial.ChordPoint), 1E-9);
                Near(new GeoPoint2(37.5, 21.6506), P2(dimension.TextPosition), 1E-3);
                Near(new GeoPoint2(36.5493, 23.2973), P2(Words(dimension).Location), 1E-3);
                HasLine(dimension, new GeoPoint2(40.6243, 23.4544), new GeoPoint2(19.4856, 11.25));
            }

            CadCommandSession free = Arial(s => s.Dimtmove = 2);
            Run(free, "DRA", new[] { Circle(free, 20) }, At30(45));
            DxfDimension moved = Made(free);
            Run(free, "DIMTEDIT", new[] { moved.Handle }, Polar(60, 50));

            // AutoCAD 2021: the words at the point, turned along the line; the line from the centre to the arrowhead inside.
            Near(Polar(60, 50), P2(Words(moved).Location), 1E-9);
            Assert.Equal(Math.PI / 6.0, Words(moved).Rotation, 9);
            HasLine(moved, P(0, 0), new GeoPoint2(15.1554, 8.75));
            Assert.Single(Lines(moved));
            ArrowAt(moved, At30(20));
        }

        [Fact]
        public void ARadiusMadeUnderDimtmove1And2_KeepsItsLineThroughThePointTheCircleWasPickedAt()
        {
            // AutoCAD 2021, the circle picked at 30 degrees and the line put 45 out at 70: under 1 the line stays at 30, the
            // words 34.47 out along it; under 2 the words at the point itself, the line inside.
            CadCommandSession session = Arial(s => s.Dimtmove = 1);
            Circle(session, 20);
            Run(session, "DRA", Polar(30, 20), Polar(70, 45));
            var slid = (DxfRadialDimension)Made(session);
            Near(At30(20), P2(slid.ChordPoint), 1E-9);
            Near(new GeoPoint2(29.8536, 17.236), P2(slid.TextPosition), 1E-3);
            Near(new GeoPoint2(28.9029, 18.8827), P2(Words(slid).Location), 1E-3);
            HasLine(slid, new GeoPoint2(32.9779, 19.0398), new GeoPoint2(19.4856, 11.25));

            session = Arial(s => s.Dimtmove = 2);
            Circle(session, 20);
            Run(session, "DRA", Polar(30, 20), Polar(70, 45));
            var free = (DxfRadialDimension)Made(session);
            Near(At30(20), P2(free.ChordPoint), 1E-9);
            Near(Polar(70, 45), P2(Words(free).Location), 1E-9);
            HasLine(free, P(0, 0), new GeoPoint2(15.1554, 8.75));

            // Under 0 the line turns to the point, as before.
            session = Arial(s => s.Dimtmove = 0);
            Circle(session, 20);
            Run(session, "DRA", Polar(30, 20), Polar(70, 45));
            Near(Polar(70, 20), P2(((DxfRadialDimension)Made(session)).ChordPoint), 1E-9);
        }

        [Fact]
        public void ADiameterMadeUnderDimtmove2_HasItsWordsAtThePoint_TheLineBrokenAboutThem()
        {
            CadCommandSession session = Arial(s => s.Dimtmove = 2);
            Run(session, "DDI", new[] { Circle(session, 20) }, At30(8));
            DxfDimension dimension = Made(session);

            // AutoCAD 2021: "%%c40" at the point, the line between the backs of the arrowheads broken 3.771 either side of it.
            Near(At30(8), P2(Words(dimension).Location), 1E-9);
            HasLine(dimension, new GeoPoint2(-15.1554, -8.75), new GeoPoint2(3.6621, 2.1143));
            HasLine(dimension, new GeoPoint2(15.1554, 8.75), new GeoPoint2(10.1943, 5.8857));
            Assert.Equal(2, Solids(dimension).Count);
        }
    }
}
