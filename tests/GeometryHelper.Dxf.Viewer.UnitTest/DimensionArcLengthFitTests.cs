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

        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Arial();
            set(document.Database.DimStyleTable["Standard"]);
            StandardCurrent(document.Database);
            return document;
        }

        [Theory]
        [InlineData(true, 0, 3.5929, 0.2268, 2.5463, 0.7503)]
        [InlineData(false, 1, -0.2268, 3.5929, -1.2416, 3.6023)]
        public void LevelOrTurnedWordsOutsideAnArcUnderDimjust1_StandPastItsSecondEnd_AndItsArcPointIsAThirdRound_AsAutoCad(
            bool level,
            int above,
            double arcX,
            double arcY,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021: an arc of 3 through 10 degrees put 3.6 out, from 0 degrees (level words, on the arc) and from 90 (turned
            // words, above it), DIMJUST 1. The words go past the second end, so the arc point stays a third of the way round
            // (6.39 degrees from the second end's side, 3.61 from the first); DIMJUST 0 and 2 put it two thirds round.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = 1;
                s.Dimtad = above;
                s.Dimtih = level;
                s.Dimtoh = level;
            });
            DxfArcDimension dimension = Dimension(document, 3.0, level ? 0.0 : 90.0, 10.0, 3.6);

            Near(Offset(arcX, arcY), dimension.ArcPoint, 1E-3);
            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Theory]
        [InlineData(2, 3.1421, 1.757, 2.9301, 2.0916)]
        [InlineData(1, 3.0927, 1.8426, 3.2764, 1.4918)]
        public void TurnedWordsBetweenTheRaysUnderDimjust1And2_StandABlockFromTheirRay_AsAutoCad(
            int justify,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, an arc of 3 through 40 degrees from 10 put 3.6 out, the words turned along the arc: they stand an
            // arrowhead and three gaps from the ray, then the arc symbol and half their width (1.0 along the arc), by the first
            // ray for DIMJUST 2 and the second for 1; the arc point is two thirds round beside the first ray's words, a third
            // beside the second's.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = justify;
                s.Dimtad = 0;
                s.Dimtih = false;
                s.Dimtoh = false;
            });
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, 40.0, 3.6);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 1E-3);
        }

        [Theory]
        [InlineData(2, 3.4385, 1.0661)]
        [InlineData(1, 3.3194, 1.3935)]
        public void LevelWordsBesideTheirRayCarriedPastTheMiddle_PutTheArcPointOnTheOtherSideOfThem_AsAutoCad(
            int justify,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, an arc of 3 through 20 degrees from 10 put 3.6 out: the level words stand 0.74 from their ray along the
            // arc, past the middle of its 16.6 degrees, so the arc point goes to the other side of them: two thirds round for
            // the words beside the second ray, a third for those beside the first.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = justify;
                s.Dimtad = 0;
            });
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, 20.0, 3.6);

            Near(Offset(arcX, arcY), dimension.ArcPoint, 1E-3);
        }

        [Fact]
        public void TheSymbolOverTheWords_TakesAboutFourTenthsOfTheirHeightMoreOfTheChord_AsAutoCad()
        {
            // AutoCAD 2021, an arc of 3 put 3.6 out, DIMARCSYM 1: the chord of 0.82 (6.28 degrees) keeps the words outside, the
            // chord of 0.88 takes them between the extension lines; with no symbol they fit from 0.82.
            CadDocument document = Styled(s =>
            {
                s.Dimarcsym = 1;
                s.Dimtad = 0;
            });
            DxfArcDimension dimension = Dimension(
                document,
                3.0,
                10.0,
                2.0 * Math.Asin(0.82 / 6.0) * 180.0 / Math.PI,
                3.6
            );

            Near(Offset(4.2045, 0.529), Words(dimension), 1E-3);
            Near(Offset(3.3821, 1.2333), dimension.ArcPoint, 1E-3);
        }

        [Fact]
        public void AnArcOfOneThroughAHundredAndTwentyDegreesDimensionedOnItself_KeepsItsArrowheadsBetweenTheRays_AsAutoCad()
        {
            // AutoCAD 2021, a chord of 1.732 on the arc itself: the words (1.42 with the symbol and the gaps) and the arrowheads
            // both stand between the extension lines, the room counted as a chord and a third of what the arc has beyond it.
            CadDocument document = Arial();
            DxfArcDimension dimension = Dimension(document, 1.0, 10.0, 120.0, 1.0);
            var arcs = dimension
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased && Math.Abs(a.Radius - 1.0) < 1E-6)
                .OrderBy(a => a.StartAngle)
                .ToList();

            Assert.Equal(2, dimension.DimBlock.OfType<DxfSolid>().Count(s => !s.IsErased));
            Assert.Equal(2, arcs.Count);
            Assert.Equal(0.3548, arcs[0].StartAngle, 3);
            Assert.Equal(0.8599, arcs[0].EndAngle, 3);
            Assert.Equal(2.0416, arcs[1].StartAngle, 3);
            Assert.Equal(2.0887, arcs[1].EndAngle, 3);
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
