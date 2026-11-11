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
    /// The picture of a linear dimension as AutoCAD 2021 lays it out from the style: DIMTAD, DIMJUST, DIMGAP, DIMTIH, DIMTOH,
    /// and what moves outside where the words or the arrowheads do not fit (DIMTIX, DIMSOXD, DIMATFIT, DIMTOFL).
    /// </summary>
    public class DimensionPictureTests
    {
        private const double Gap = 0.625;
        private const double Height = 2.5;
        private const double Arrow = 2.5;
        private const double Lift = Gap + Height / 2.0;

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
            style.Dimtad = 1;
            style.Dimtih = false;
            style.Dimtoh = false;
            style.Dimtofl = true;
            style.Dimatfit = 3;
            set(style);
            return Session(document);
        }

        private static DxfDimension Made(CadCommandSession session) =>
            session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        /// <summary>The lines of a picture on the level y, its dimension line, ordered from the left.</summary>
        private static List<DxfLine> OnLevel(DxfDimension dimension, double y) =>
            dimension
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && Math.Abs(l.StartPoint.Y - y) < 1E-9 && Math.Abs(l.EndPoint.Y - y) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();

        private static double Left(DxfLine line) => Math.Min(line.StartPoint.X, line.EndPoint.X);

        private static double Right(DxfLine line) => Math.Max(line.StartPoint.X, line.EndPoint.X);

        private static IEnumerable<double> SolidXs(DxfDimension dimension) =>
            dimension
                .DimBlock.OfType<DxfSolid>()
                .SelectMany(s => new[] { s.GetPointAt(0).X, s.GetPointAt(1).X, s.GetPointAt(2).X });

        [Fact]
        public void WithDimtad0_TheWordsStandOnTheLine_AndTheLineIsBrokenAboutThemByTheGap()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 0);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(50, -30, 0), Words(dimension).Location);
            List<DxfLine> line = OnLevel(dimension, -30);
            Assert.Equal(2, line.Count);

            // From the back of each arrowhead, as AutoCAD 2021 draws the line: 2.5 to 97.5.
            Assert.Equal(Arrow, Left(line[0]), 9);
            Assert.Equal(100 - Arrow, Right(line[1]), 9);
            Assert.Equal(100, Right(line[0]) + Left(line[1]), 9);
            Assert.True(Right(line[0]) < 50 - Gap);
            AssertReopens(session.Document);
        }

        [Fact]
        public void WithDimtad1_TheWordsStandAboveTheWholeLine_ByTheGapAndHalfTheirHeight()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 1);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));

            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(50, -30 + Lift, 0), Words(dimension).Location);
            DxfLine line = Assert.Single(OnLevel(dimension, -30));
            Assert.Equal(Arrow, Left(line), 9);
            Assert.Equal(100 - Arrow, Right(line), 9);
        }

        [Fact]
        public void WithDimtad2_TheWordsStandOnTheSideAwayFromThePointsMeasured_AndWithDimtad4_Below()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 2);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Assert.Equal(-30 - Lift, Words(Made(session)).Location.Y, 9);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 30));
            Assert.Equal(30 + Lift, Words(Made(session)).Location.Y, 9);

            session = Metric(s => s.Dimtad = 4);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 30));
            Assert.Equal(30 - Lift, Words(Made(session)).Location.Y, 9);

            // JIS (3) puts them above, as 1 does.
            session = Metric(s => s.Dimtad = 3);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Assert.Equal(-30 + Lift, Words(Made(session)).Location.Y, 9);
        }

        [Fact]
        public void Dimjust1And2_PutTheWordsNextToTheFirstOrTheSecondExtensionLine()
        {
            CadCommandSession session = Metric(s => s.Dimjust = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            DxfMText first = Words(Made(session));

            session = Metric(s => s.Dimjust = 2);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            DxfMText second = Words(Made(session));

            Assert.True(first.Location.X > Arrow + Gap);
            Assert.True(first.Location.X < 25);
            Assert.Equal(100, first.Location.X + second.Location.X, 9);
            Assert.Equal(-30 + Lift, first.Location.Y, 9);
        }

        [Fact]
        public void Dimjust3And4_PutTheWordsOverAnExtensionLine_AlongIt_ItsLineRunOnUnderThem()
        {
            CadCommandSession session = Metric(s => s.Dimjust = 3);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            DxfDimension dimension = Made(session);
            DxfMText words = Words(dimension);

            // The first extension line runs down from (0, 0): the words read upward on its left, past its end.
            Assert.Equal(Math.PI / 2.0, words.Rotation, 9);
            Assert.Equal(-Lift, words.Location.X, 9);
            Assert.True(words.Location.Y < -30 - 1.25);
            List<DxfLine> upright = dimension
                .DimBlock.OfType<DxfLine>()
                .Where(l => Math.Abs(l.StartPoint.X - l.EndPoint.X) < 1E-9)
                .OrderBy(l => l.StartPoint.X)
                .ToList();
            Assert.Equal(2, upright.Count);
            Assert.True(
                Math.Abs(upright[0].EndPoint.Y - upright[0].StartPoint.Y)
                    > Math.Abs(upright[1].EndPoint.Y - upright[1].StartPoint.Y) + 1.0
            );

            session = Metric(s => s.Dimjust = 4);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Assert.Equal(100 - Lift, Words(Made(session)).Location.X, 9);
        }

        [Fact]
        public void WhereOnlyTheWordsFit_Dimatfit3PutsTheArrowheadsOutside_AndDimtoflSaysWhetherTheLineIsDrawnBetween()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimtofl = false;
            });
            Run(session, "DLI", P(0, 0), P(5, 0), P(2.5, -30));
            DxfDimension dimension = Made(session);

            Near(new GeoPoint3(2.5, -30, 0), Words(dimension).Location);
            Assert.All(SolidXs(dimension), x => Assert.True(x <= 1E-9 || x >= 5 - 1E-9));
            List<DxfLine> line = OnLevel(dimension, -30);
            Assert.Equal(2, line.Count);
            Assert.Equal(-2 * Arrow, Left(line[0]), 9);
            Assert.Equal(-Arrow, Right(line[0]), 9);
            Assert.Equal(5 + Arrow, Left(line[1]), 9);

            session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimtofl = true;
            });
            Run(session, "DLI", P(0, 0), P(5, 0), P(2.5, -30));
            Assert.Contains(OnLevel(Made(session), -30), l => Math.Abs(Left(l)) < 1E-9);
        }

        [Fact]
        public void WhereNeitherFits_Dimatfit0MovesBothOutside_PastTheSecondExtensionLine_TheLineRunOnToTheWords()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 1;
                s.Dimatfit = 0;
            });
            Run(session, "DLI", P(0, 0), P(5, 0), P(2.5, -30));
            DxfDimension dimension = Made(session);

            DxfMText words = Words(dimension);
            Assert.True(words.Location.X > 5 + 2 * Arrow);
            Assert.Equal(-30 + Lift, words.Location.Y, 9);
            Assert.All(SolidXs(dimension), x => Assert.True(x <= 1E-9 || x >= 5 - 1E-9));
            Assert.Contains(OnLevel(dimension, -30), l =>
                Math.Abs(Left(l) - (5 + Arrow)) < 1E-9 && Right(l) > words.Location.X
            );
        }

        [Fact]
        public void Dimatfit1MovesTheArrowheadsFirst_And2TheWordsFirst()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimatfit = 1;
            });
            Run(session, "DLI", P(0, 0), P(7, 0), P(3.5, -30));
            DxfDimension arrowsFirst = Made(session);
            Assert.Equal(3.5, Words(arrowsFirst).Location.X, 9);
            Assert.All(SolidXs(arrowsFirst), x => Assert.True(x <= 1E-9 || x >= 7 - 1E-9));

            session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimatfit = 2;
            });
            Run(session, "DLI", P(0, 0), P(7, 0), P(3.5, -30));
            DxfDimension wordsFirst = Made(session);
            Assert.True(Words(wordsFirst).Location.X > 7);
            Assert.All(SolidXs(wordsFirst), x => Assert.True(x >= -1E-9 && x <= 7 + 1E-9));
        }

        [Fact]
        public void Dimtix_KeepsTheWordsInside_AndDimsoxdThenLeavesOutArrowheadsThatDoNotFit()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimatfit = 0;
                s.Dimtix = true;
            });
            Run(session, "DLI", P(0, 0), P(2, 0), P(1, -30));
            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(1, -30, 0), Words(dimension).Location);
            Assert.Equal(2, dimension.DimBlock.OfType<DxfSolid>().Count());

            session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimtix = true;
                s.Dimsoxd = true;
            });
            Run(session, "DLI", P(0, 0), P(2, 0), P(1, -30));
            Assert.Empty(Made(session).DimBlock.OfType<DxfSolid>());
        }

        [Fact]
        public void Dimtih_KeepsWordsInsideLevel_OnASlantingLine_WhereTheyStandOnIt()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 1;
                s.Dimtih = true;
            });
            Run(session, "DAL", P(0, 0), P(60, 60), P(-20, 20));
            DxfDimension dimension = Made(session);
            DxfMText words = Words(dimension);

            Assert.Equal(0.0, words.Rotation, 12);

            // On the line through (-20, 20) at 45 degrees: square to it the words stand nowhere off it.
            Assert.Equal(0.0, (words.Location.X + 20) * Math.Sqrt(0.5) - (words.Location.Y - 20) * Math.Sqrt(0.5), 9);
            Assert.Equal(4, dimension.DimBlock.OfType<DxfLine>().Count(l => l.Layer != "Defpoints"));
        }

        [Fact]
        public void Dimtoh_KeepsWordsOutsideLevel()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimatfit = 0;
                s.Dimtoh = true;
            });
            Run(session, "DAL", P(0, 0), P(3, 3), P(-20, 20));
            Assert.Equal(0.0, Words(Made(session)).Rotation, 12);

            session = Metric(s =>
            {
                s.Dimatfit = 0;
                s.Dimtoh = false;
            });
            Run(session, "DAL", P(0, 0), P(3, 3), P(-20, 20));
            Assert.Equal(Math.PI / 4.0, Words(Made(session)).Rotation, 9);
        }

        [Fact]
        public void Dimatfit3_KeepsTheArrowheadsInsideWhereTheyFit_AndMovesTheWordsOut()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimatfit = 3;
            });
            Run(session, "DLI", P(0, 0), P(7, 0), P(3.5, -30));
            DxfDimension dimension = Made(session);

            Assert.True(Words(dimension).Location.X > 7 + Arrow + Gap);
            Assert.All(SolidXs(dimension), x => Assert.True(x >= -1E-9 && x <= 7 + 1E-9));
        }

        [Fact]
        public void WordsForcedInsideThatDoNotFit_LeaveTheLineWhole()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimtix = true;
                s.Dimtofl = true;
            });
            Run(session, "DLI", P(0, 0), P(2, 0), P(1, -30));

            Assert.Contains(OnLevel(Made(session), -30), l =>
                Math.Abs(Left(l)) < 1E-9 && Math.Abs(Right(l) - 2) < 1E-9
            );
        }

        [Fact]
        public void WordsKeptLevelOutsideASlantingLine_StandOnALevelLanding()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 1;
                s.Dimatfit = 0;
                s.Dimtoh = true;
            });
            Run(session, "DAL", P(0, 0), P(3, 3), P(-20, 20));
            DxfDimension dimension = Made(session);
            DxfMText words = Words(dimension);

            DxfLine landing = Assert.Single(dimension.DimBlock.OfType<DxfLine>(), l =>
                l.Layer != "Defpoints" && Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1E-9
            );
            Assert.Equal(Lift, words.Location.Y - landing.StartPoint.Y, 9);
            Assert.Equal(Gap, (words.Location.X - Left(landing)) - (Right(landing) - words.Location.X), 9);
            Assert.True(words.Location.X > Left(landing) && words.Location.X < Right(landing));
        }

        [Fact]
        public void Dimjust1OnAShortLine_PutsTheWordsInTheMiddle_And3WithDimtad0_OnTheExtensionLine()
        {
            CadCommandSession session = Metric(s =>
            {
                s.Dimtad = 1;
                s.Dimjust = 1;
            });
            Run(session, "DLI", P(0, 0), P(10, 0), P(5, -30));
            Assert.Equal(5, Words(Made(session)).Location.X, 9);

            session = Metric(s =>
            {
                s.Dimtad = 0;
                s.Dimjust = 3;
            });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            DxfDimension dimension = Made(session);
            Assert.Equal(0, Words(dimension).Location.X, 9);
            Assert.Contains(dimension.DimBlock.OfType<DxfLine>(), l =>
                Math.Abs(l.StartPoint.X) < 1E-9
                && Math.Abs(l.EndPoint.X) < 1E-9
                && Math.Abs(l.EndPoint.Y - (-30 - 1.25)) < 1E-9
            );
        }

        [Fact]
        public void DimscaleTyped_IsTakenByNewDimensions_AsAnOverrideOfTheirStyle_AsAutoCad()
        {
            // The style's DIMSCALE is 1; DIMSCALE 10 typed makes the header's 10, which a new dimension takes, as AutoCAD 2021's.
            CadCommandSession session = Metric(_ => { });
            CadDocument document = session.Document;
            Run(session, "DIMSCALE", "10", "DLI", P(0, 0), P(300, 0), P(150, -30));

            DxfDimension dimension = Made(session);
            Assert.Equal(10.0, document.Database.Dimscale);
            Assert.Equal(1.0, document.Database.DimStyleTable["Standard"].Dimscale);
            Assert.Equal(10.0, dimension.Dimscale);
            Assert.Equal(Height * 10.0, Words(dimension).TextHeight, 9);
            Assert.Equal(
                new[] { 0.0, Arrow * 10.0, 300.0 - Arrow * 10.0, 300.0 },
                SolidXs(dimension).Select(x => Math.Round(x, 9)).Distinct().OrderBy(x => x)
            );
            AssertReopens(document);

            // DIMBASELINE puts the next line DIMDLI times the current DIMSCALE further out.
            double dli = document.Database.DimStyleTable["Standard"].Dimdli;
            Run(session, "DIMBASELINE", P(400, 0), Esc);
            DxfRotatedDimension next = Assert.IsType<DxfRotatedDimension>(Made(session));
            Assert.Equal(10.0, next.Dimscale);
            Assert.Equal(-30.0 - dli * 10.0, next.DimLinePoint.Y, 9);
        }

        [Fact]
        public void DimscaleOfTheStyle_WritesNoOverride()
        {
            CadCommandSession session = Metric(_ => { });
            Run(session, "DIMSCALE", "1", "DLI", P(0, 0), P(300, 0), P(150, -30));

            Assert.Equal(1.0, Made(session).Dimscale);
            Assert.DoesNotContain("DSTYLE", session.Document.Database.ToText());
        }

        [Fact]
        public void AStyleChanged_DrawsItsDimensionsAgainByTheNewLayout()
        {
            CadCommandSession session = Metric(s => s.Dimtad = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            CadDocument document = session.Document;
            string style = CadDimStyleEditor.Current(document.Database);
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(document.Database.DimStyleTable[style]);
            values["Dimtad"] = 0;

            Assert.True(CadDimStyleEditor.Apply(document, style, values).Succeeded);

            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(50, -30, 0), Words(dimension).Location);
            Assert.Equal(2, OnLevel(dimension, -30).Count);
        }

        private static bool HasLine(DxfDimension dimension, GeoPoint2 a, GeoPoint2 b) =>
            dimension
                .DimBlock.OfType<DxfLine>()
                .Any(l =>
                    !l.IsErased
                    && (
                        (Same(l.StartPoint, a) && Same(l.EndPoint, b)) || (Same(l.StartPoint, b) && Same(l.EndPoint, a))
                    )
                );

        private static bool Same(GeoPoint3 p, GeoPoint2 q) => Math.Abs(p.X - q.X) < 1E-6 && Math.Abs(p.Y - q.Y) < 1E-6;

        [Fact]
        public void WordsTheFitMovesOut_StandOverTheLineUnderDimtmove2_AndPastALeaderUnder1()
        {
            // AutoCAD 2021: a dimension of 4 with its line 20 below, its words "4" moved out under DIMTMOVE 2, has them 1.25 +
            // 0.9375 + 1.25 + 2.5 = 5.9375 below the middle of the line, on the side away from the points; no line runs on.
            CadCommandSession session = Metric(s =>
            {
                s.Dimatfit = 0;
                s.Dimtmove = 2;
            });
            Run(session, "DLI", P(0, 0), P(4, 0), P(2, -20));
            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(2, -25.9375, 0), Words(dimension).Location);
            Assert.DoesNotContain(OnLevel(dimension, -20), l => Right(l) > 4 + 2 * Arrow + 1E-9);

            // Under DIMTMOVE 1 a leader from the middle of the line to a third as far along again, 1.78125, and a landing as wide
            // as the words and a gap each side, the words above it; the line between the extension lines drawn twice.
            session = Metric(s =>
            {
                s.Dimatfit = 0;
                s.Dimtmove = 1;
            });
            Run(session, "DLI", P(0, 0), P(4, 0), P(2, -20));
            dimension = Made(session);
            Assert.True(HasLine(dimension, P(2, -20), P(3.78125, -25.9375)));
            Assert.True(HasLine(dimension, P(3.78125, -25.9375), P(3.78125 + 1.75 + 2 * Gap, -25.9375)));
            Near(new GeoPoint3(3.78125 + Gap + 0.875, -25.9375 + Lift, 0), Words(dimension).Location);
            Assert.Equal(2, OnLevel(dimension, -20).Count(l => Left(l) == 0 && Right(l) == 4));
        }

        [Fact]
        public void DimteditUnderDimtmove1_DrawsALeaderToWordsFarFromTheLine_AndUnder2PutsTheWordsAtThePoint()
        {
            // AutoCAD 2021: "100" moved to 10 above the line, 10 left of its middle: a leader from the middle to 0.625 short of the
            // point, a landing on to the left as wide as the words and a gap each side, the words above it, starting at the point.
            CadCommandSession session = Metric(s => s.Dimtmove = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            string handle = Made(session).Handle;
            Run(session, "DIMTEDIT", new[] { handle }, P(40, -10));
            DxfDimension dimension = Made(session);
            Assert.Equal(-20, ((DxfRotatedDimension)dimension).DimLinePoint.Y, 9);
            Assert.True(HasLine(dimension, P(50, -20), P(40.625, -10)));
            Assert.True(HasLine(dimension, P(40.625, -10), P(40.625 - 5.25 - 2 * Gap, -10)));
            Near(new GeoPoint3(40 - 2.625, -10 + Lift, 0), Words(dimension).Location);

            // Within the gap and half the heights of the words and the arrowheads, 3.125, no leader: the words above the point.
            Run(session, "DIMTEDIT", new[] { handle }, P(40, -17));
            dimension = Made(session);
            Assert.False(HasLine(dimension, P(50, -20), P(40.625, -17)));
            Near(new GeoPoint3(40, -17 + Lift, 0), Words(dimension).Location);

            // DIMTMOVE 2: the words' middle at the point, the line broken where it runs through them.
            session = Metric(s => s.Dimtmove = 2);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "DIMTEDIT", new[] { Made(session).Handle }, P(30, -20));
            dimension = Made(session);
            Near(new GeoPoint3(30, -20, 0), Words(dimension).Location);
            Assert.Equal(2, OnLevel(dimension, -20).Count);
            Assert.Equal(30 - 2.625 - Gap, Right(OnLevel(dimension, -20)[0]), 9);
        }

        [Fact]
        public void DimteditUnderDimtad0_PutsTheLineThroughThePoint_AndRunsItOnToAGapShortOfWordsPastAnEnd()
        {
            // AutoCAD 2021: "100" moved to (120, -25) past the second extension line: the line at -25, run on from 100 to a gap
            // short of the words, 120 - 2.625 - 0.625.
            CadCommandSession session = Metric(s => s.Dimtad = 0);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "DIMTEDIT", new[] { Made(session).Handle }, P(120, -25));
            DxfDimension dimension = Made(session);
            Near(new GeoPoint3(120, -25, 0), Words(dimension).Location);
            Assert.True(HasLine(dimension, P(100, -25), P(120 - 2.625 - Gap, -25)));

            // Along the line, the line broken about the words a gap clear of them.
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, P(30, -20));
            dimension = Made(session);
            Near(new GeoPoint3(30, -20, 0), Words(dimension).Location);
            List<DxfLine> line = OnLevel(dimension, -20);
            Assert.Equal(2, line.Count);
            Assert.Equal(30 - 2.625 - Gap, Right(line[0]), 9);
            Assert.Equal(30 + 2.625 + Gap, Left(line[1]), 9);
        }
    }
}
