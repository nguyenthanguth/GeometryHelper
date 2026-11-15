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
    /// Arc length dimensions (DIMARC) laid out as AutoCAD 2021 does where the last follow-ups left them: the arrowheads that
    /// go outside with the words on an arc of more than half a circle. The numbers are those AutoCAD 2021's console gave for
    /// the same drawings, made with its own template (Standard in arial.ttf, text 0.18 high, arrowheads 0.18, gap 0.09, words
    /// kept level by DIMTIH and DIMTOH, DIMDEC 4); positions are given from the arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps10Tests
    {
        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimtxsty = standard;
            style.Dimdec = 4;
            set(style);
            StandardCurrent(document.Database);
            return document;
        }

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static GeoPoint3 Offset(double dx, double dy) => new GeoPoint3(100 + dx, 100 + dy, 0);

        private static DxfArcDimension Dimension(
            CadDocument document,
            double radius,
            double from,
            double sweep,
            double distance
        )
        {
            CadCommandSession session = Session(document);
            Run(
                session,
                "ARC",
                "C",
                P(100, 100),
                Polar(radius, from),
                "A",
                sweep.ToString("0.##########", CultureInfo.InvariantCulture)
            );
            double middle = from + sweep / 2.0;
            Run(session, "DIMARC", Polar(radius, middle), Polar(distance, middle));
            return (DxfArcDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
        }

        private static GeoPoint3 Words(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfMText>().First().Location;

        private static double AngleAbout(double x, double y) => Math.Atan2(y - 100.0, x - 100.0) * 180.0 / Math.PI;

        /// <summary>How many arrowheads have their backs on the arc, between its first end and its second, going round.</summary>
        private static int ArrowheadsOnTheArc(DxfDimension dimension, double from, double sweep)
        {
            int inside = 0;

            foreach (DxfSolid solid in dimension.DimBlock.OfType<DxfSolid>().Where(s => !s.IsErased))
            {
                GeoPoint3[] c = { solid.GetPointAt(0), solid.GetPointAt(1), solid.GetPointAt(2) };
                int tip = Enumerable.Range(0, 3).OrderBy(k => c[(k + 1) % 3].DistanceTo(c[(k + 2) % 3])).First();
                GeoPoint3 a = c[(tip + 1) % 3];
                GeoPoint3 b = c[(tip + 2) % 3];
                double back = AngleAbout((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                double along = back - from - Math.Floor((back - from) / 360.0) * 360.0;

                if (along > 0.0 && along < sweep)
                {
                    inside++;
                }
            }

            return inside;
        }

        [Theory]
        [InlineData(2, 10.0, 2)]
        [InlineData(2, 20.0, 0)]
        [InlineData(2, 30.0, 0)]
        [InlineData(2, 40.0, 2)]
        [InlineData(0, 0.0, 2)]
        [InlineData(0, 10.0, 0)]
        [InlineData(0, 30.0, 0)]
        [InlineData(0, 40.0, 2)]
        public void LevelWordsOutsideAnArcPastAHalfCircle_SendTheArrowheadsOutWhereTheirBoxComesNearOneAndTheMiddlePointsDown_AsAutoCad(
            int symbol,
            double from,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 through 181 degrees put 0.45 out: the words (3.1590 and so on) outside past the first
            // end took both arrowheads outside with them from 10 to 30 degrees with the symbol before them and from 20 to 30
            // without, where the arc's middle points at 101 to 121 degrees (at 91 and at 131 the arrowheads stayed in), and the
            // same half a turn on, as the box of the words (the symbol counted) stands within 0.9 gaps of an arrowhead there.
            CadDocument document = Styled(s => s.Dimarcsym = symbol);
            DxfArcDimension dimension = Dimension(document, 1.0, from, 181.0, 0.45);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, from, 181.0));
        }

        [Theory]
        [InlineData(0.30, 2, -0.3530, -0.0772)]
        [InlineData(0.30, 0, -0.6844, -0.0772)]
        public void LevelWordsWithTheirArrowheadsOutsideAnArcPastAHalfCircle_StandAnArrowheadFromTheArcWhereTheyStoodWithThemIn_AsAutoCad(
            double distance,
            int symbol,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, the arc of 1 from 20 degrees through 181, put 0.3 out: the arrowheads outside, back to back before
            // the first end, and the words past a landing 0.18 along the arc from that end (14.9 degrees before it), not two
            // arrowheads, as for the words of an arc under a half circle.
            CadDocument document = Styled(s => s.Dimarcsym = symbol);
            DxfArcDimension dimension = Dimension(document, 1.0, 20.0, 181.0, distance);

            Assert.Equal(0, ArrowheadsOnTheArc(dimension, 20.0, 181.0));
            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
        }

        [Theory]
        [InlineData(0.36, 0.045, 1.32, false)]
        [InlineData(0.36, 0.045, 1.34, true)]
        [InlineData(0.18, 0.18, 0.99, false)]
        [InlineData(0.18, 0.18, 1.03, true)]
        public void TheRoomTheSymbolTakesOnAWideSweepFollowsTheGapAsWellAsTheText_AsAutoCad(
            double text,
            double gap,
            double distance,
            bool wordsInside
        )
        {
            // AutoCAD 2021, the arc of 1 through 120 degrees from 10, DIMARCSYM 0, the words first stood in the middle of the
            // arc from 1.34 out for text 0.36 and DIMGAP 0.045 (the room before took them from 1.30), from 1.01 out for text
            // 0.18 and DIMGAP 0.18 (1.04): the room the symbol takes grows by 4.3 units for each unit DIMGAP is short of half
            // the text's height, and DIMASZ takes no part.
            CadDocument document = Styled(s =>
            {
                s.Dimarcsym = 0;
                s.Dimtxt = text;
                s.Dimgap = gap;
            });
            DxfArcDimension dimension = Dimension(document, 1.0, 10.0, 120.0, distance);
            GeoPoint3 words = Words(dimension);
            double along = AngleAbout(words.X, words.Y) - 10.0;
            along -= Math.Floor(along / 360.0) * 360.0;

            Assert.Equal(wordsInside, along < 120.0);
        }

        [Theory]
        [InlineData(98.0, 1.14, 0)]
        [InlineData(98.0, 1.17, 2)]
        [InlineData(100.0, 1.12, 0)]
        [InlineData(100.0, 1.15, 2)]
        public void LevelWordsWithTheSymbolOnAnArcJustPast90Degrees_NeedLessRoomForBothArrowheadsThanOnOneOf100_AsAutoCad(
            double sweep,
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 from 25 degrees through 98 and 100 put 0.01 apart, DIMARCSYM 0: both arrowheads came in
            // from 1.16 and 1.14 out, where the fit of 0.45 of the words' height more room in the window of the arc's middle
            // direction had them at 1.19 and 1.17; 0.2475 of it at 100 degrees is the amount (the arcs probed before were put at
            // steps of 0.02).
            CadDocument document = Styled(s => s.Dimarcsym = 0);
            DxfArcDimension dimension = Dimension(document, 1.0, 25.0, sweep, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 25.0, sweep));
        }

        [Theory]
        [InlineData(0.66, 0)]
        [InlineData(0.72, 2)]
        public void AWiderGapKeepsTheArrowheadsOutLonger_AsAutoCad(double distance, int arrowheadsInside)
        {
            // AutoCAD 2021, the arc of 1 from 0 through 204 degrees, DIMGAP 0.18, the symbol before the words: the words stand
            // outside up to 0.76 out, the arrowheads outside with them up to 0.68 (up to 0.42 for DIMGAP 0.09), as the
            // distance their box comes to an arrowhead that stands in goes with the gap.
            CadDocument document = Styled(s =>
            {
                s.Dimarcsym = 0;
                s.Dimgap = 0.18;
            });
            DxfArcDimension dimension = Dimension(document, 1.0, 0.0, 204.0, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 0.0, 204.0));
        }
    }
}
