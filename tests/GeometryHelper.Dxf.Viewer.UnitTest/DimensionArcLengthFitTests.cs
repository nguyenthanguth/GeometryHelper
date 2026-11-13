using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The words of an arc length dimension on a small arc, as AutoCAD 2021 lays them out: whether they fit between the
    /// extension lines (level words are measured across the arc's turn), and where they stand outside. The numbers are those
    /// AutoCAD 2021's console gave for the same drawings, made with its own template (Standard in arial.ttf, text 0.18 high,
    /// words kept level by DIMTIH and DIMTOH); positions are given from the arc's centre.
    /// </summary>
    public class DimensionArcLengthFitTests
    {
        private static CadDocument Arial()
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            StandardCurrent(document.Database);
            return document;
        }

        private static GeoPoint2 Polar(double x, double y, double radius, double degrees) =>
            P(x + radius * Math.Cos(degrees * Math.PI / 180.0), y + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static DxfArcDimension Dimension(
            CadDocument document,
            double radius,
            double from,
            double sweep,
            double distance
        )
        {
            const double x = 100;
            const double y = 100;
            CadCommandSession session = Session(document);
            Run(
                session,
                "ARC",
                "C",
                P(x, y),
                Polar(x, y, radius, from),
                "A",
                sweep.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            double middle = from + sweep / 2.0;
            Run(session, "DIMARC", Polar(x, y, radius, middle), Polar(x, y, distance, middle));
            return (DxfArcDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
        }

        private static GeoPoint3 Offset(double dx, double dy) => new GeoPoint3(100 + dx, 100 + dy, 0);

        private static GeoPoint3 Words(DxfArcDimension dimension) =>
            dimension.DimBlock.OfType<DxfMText>().First().Location;

        [Theory]
        [InlineData(0.8, 2.3102, 0.6552, 2.2553, 0.8208)]
        [InlineData(1.0, 2.874, 0.8604, 2.8191, 1.0261)]
        [InlineData(1.2, 3.4385, 1.0661, 3.3829, 1.2313)]
        [InlineData(2.0, 5.6952, 1.8879, 5.6382, 2.0521)]
        public void AnArcOfThreeThroughTwentyDegrees_KeepsItsWordsBetweenTheExtensionLines_AsAutoCad(
            double times,
            double arcX,
            double arcY,
            double wordsX,
            double wordsY
        )
        {
            // The chord of 20 degrees on 3 is 1.0419: the level words "1.0472" and the arc symbol, 1.4 along their own width,
            // take 0.95 of it as the arc stands (its middle at 20 degrees), so they stand inside; the arc point is a third
            // of the way along, as for words that fit.
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, 20.0, 3.0 * times);

            Near(Offset(arcX, arcY), dimension.ArcPoint, 1E-3);
            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Theory]
        [InlineData(10.0, 4.0115, 12.7934, 1.9754)]
        [InlineData(10.0, 5.2731, 11.7093, 2.6252)]
        [InlineData(145.0, 6.0762, -8.8206, 6.9433)]
        [InlineData(145.0, 7.1092, -10.2376, 6.2602)]
        [InlineData(0.0, 2.7505, 12.9564, -0.132)]
        [InlineData(0.0, 3.7822, 11.9935, 0.396)]
        public void LevelWordsOnAnArcOfTen_FitWhereTheirBoxAcrossTheTurnTakesTheChord_AsAutoCad(
            double from,
            double sweep,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, arcs of 10 put 12 out, chords of 0.70 and 0.92 from 10 degrees, 1.06 and 1.24 from 145, 0.48 and 0.66
            // from 0: the words (1.44 wide with the symbol and the gaps, 0.54 high) stand level, so what they take of the chord
            // is their width times the sine of the arc's middle direction and their height times its cosine, joined at 45
            // degrees: 0.81 at 12.4 degrees, 1.15 at 148.3, 0.57 at 1.7. The first of each pair stands outside, the second between.
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 10.0, from, sweep, 12.0);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Theory]
        [InlineData(1.0, 20.0, 1.2, 2.1517, -0.1166)]
        [InlineData(1.0, 40.0, 1.2, 2.1528, 0.1012)]
        [InlineData(1.0, 5.0, 2.0, 2.9744, 0.0322)]
        [InlineData(3.0, 5.0, 1.0, 3.9702, 0.1633)]
        public void LevelWordsStandingOutsideASmallArc_StandPastItsFirstEndAfterTheArcSymbol_AsAutoCad(
            double radius,
            double sweep,
            double times,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021: arcs from 10 degrees, put 1 to 2 times out. The words run out to the right from the first end (the
            // arc starts above the horizontal), a landing an arrowhead long, a gap, the room of the arc symbol, then the
            // words.
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, radius, 10.0, sweep, radius * times);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Fact]
        public void AnArcLengthDimensionOfASmallArcScaledByFive_IsDrawnOnItsMovedInArcWithTheLengthOfTheScaledArc_AsAutoCad()
        {
            // AutoCAD 2021: the arc of 1 through 20 degrees from 10, dimensioned 1.2 out, scaled by 5 about its centre: the
            // dimension's arc is the arc of 5 moved in along the middle until it stands 1.2 from the centre (its centre at
            // (-3.572, -1.300) from the arc's), the words "1.7453" at (1.127, 0.410). The arc point is not within the sector
            // of the arc as seen from the centre (it is at 6 degrees, the sector from 10 to 30), which is no sign that the
            // rest of the circle is measured.
            CadDocument document = Arial();
            DxfArcDimension made = Dimension(document, 1.0, 10.0, 20.0, 1.2);
            string handle = made.Handle;
            string arc = document.ModelSpace.Block.OfType<DxfArc>().Last().Handle;

            document.Selection.Set(new[] { arc });
            Run(Session(document), "SCALE", P(100, 100), "5");
            DxfArcDimension dimension = Entity<DxfArcDimension>(document, handle);

            Near(Offset(1.127, 0.41), Words(dimension), 5E-3);
            Assert.Equal("1.7453", dimension.DimBlock.OfType<DxfMText>().First().Contents);
            DxfArc moved = dimension.DimBlock.OfType<DxfArc>().First(a => Math.Abs(a.Radius - 5.0) < 1E-6);
            Near(Offset(-3.572, -1.3), moved.Center, 5E-3);
        }

        private static bool HasLine(DxfArcDimension dimension, GeoPoint3 a, GeoPoint3 b) =>
            dimension
                .DimBlock.OfType<DxfLine>()
                .Any(l =>
                    (l.StartPoint.DistanceTo(a) < 1E-3 && l.EndPoint.DistanceTo(b) < 1E-3)
                    || (l.StartPoint.DistanceTo(b) < 1E-3 && l.EndPoint.DistanceTo(a) < 1E-3)
                );

        [Fact]
        public void ADimensionPutOnItsArcWhereTheArrowheadsFit_DrawsItsExtensionLinesInFromTheEnds_AsAutoCad()
        {
            // AutoCAD 2021: the arc of 3 through 20 degrees from 10, dimensioned on the arc itself (3 out), has its extension lines
            // from DIMEXO in to DIMEXE in from each end, along the middle of the arc: (2.896, 0.500) to (2.785, 0.459) from
            // the end at 10 degrees (2.954, 0.521).
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, 20.0, 3.0);

            Assert.True(HasLine(dimension, Offset(2.896, 0.5), Offset(2.785, 0.459)));
            Assert.True(HasLine(dimension, Offset(2.539, 1.479), Offset(2.429, 1.438)));
        }

        [Fact]
        public void ADimensionPutOnItsArcWhereTheArrowheadsDoNotFit_DrawsItsExtensionLinesOutFromTheEnds_AsAutoCad()
        {
            // AutoCAD 2021: the arc of 1 through 5 degrees from 10, dimensioned on the arc itself (1 out), has a chord of 0.087
            // between its ends, less than two arrowheads (0.36), and its extension lines run out from DIMEXO to DIMEXE past each
            // end: (1.046, 0.187) to (1.160, 0.213) from the end at 10 degrees (0.985, 0.174).
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 1.0, 10.0, 5.0, 1.0);

            Assert.True(HasLine(dimension, Offset(1.046, 0.187), Offset(1.16, 0.213)));
            Assert.True(HasLine(dimension, Offset(1.027, 0.272), Offset(1.142, 0.298)));
        }

        [Theory]
        [InlineData(200.0, -4.399, -1.1107)]
        [InlineData(100.0, 0.4755, 3.5652)]
        public void LevelWordsOutsideAnArcStartingBelowTheHorizontal_StandToTheLeft_AsAutoCad(
            double from,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, an arc of 3 through 10 degrees put 3.6 out: from 200 degrees the first end points down and left
            // and the words run out to the left, the room of the arc symbol still between the landing and them; from 100
            // degrees they run out to the right.
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 3.0, from, 10.0, 3.6);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }
    }
}
