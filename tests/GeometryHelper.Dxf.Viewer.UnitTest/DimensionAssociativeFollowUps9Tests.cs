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
    /// Arc length dimensions (DIMARC) laid out as AutoCAD 2021 does where the last follow-ups left them: arcs of more than half
    /// a circle, the words next to a ray (DIMJUST 1 and 2) whose box meets an arrowhead, and the room both arrowheads want on
    /// sweeps just over 90 degrees. The numbers are those AutoCAD 2021's console gave for the same drawings, made with its
    /// own template (Standard in arial.ttf, text 0.18 high, arrowheads 0.18, gap 0.09, words kept level by DIMTIH and DIMTOH,
    /// DIMDEC 4); positions are given from the arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps9Tests
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

        /// <summary>How many of the two arrowheads have their backs between their tips: standing on the arc, not past its ends.</summary>
        private static int ArrowheadsInside(DxfDimension dimension)
        {
            var tips = new double[2];
            var backs = new double[2];
            DxfSolid[] solids = dimension.DimBlock.OfType<DxfSolid>().Where(s => !s.IsErased).ToArray();
            Assert.Equal(2, solids.Length);

            for (int i = 0; i < 2; i++)
            {
                GeoPoint3[] c = { solids[i].GetPointAt(0), solids[i].GetPointAt(1), solids[i].GetPointAt(2) };

                // The arrowhead's tip is the corner opposite its shortest side, the back the middle of that side.
                int tip = Enumerable.Range(0, 3).OrderBy(k => c[(k + 1) % 3].DistanceTo(c[(k + 2) % 3])).First();
                GeoPoint3 a = c[(tip + 1) % 3];
                GeoPoint3 b = c[(tip + 2) % 3];
                tips[i] = AngleAbout(c[tip].X, c[tip].Y);
                backs[i] = AngleAbout((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
            }

            double span = Math.IEEERemainder(tips[1] - tips[0], 360.0);
            int inside = 0;

            foreach (double back in backs)
            {
                double along = Math.IEEERemainder(back - tips[0], 360.0);

                if (along / span > 0.0 && along / span < 1.0)
                {
                    inside++;
                }
            }

            return inside;
        }

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
        [InlineData(140.0, 0.4397, 0.4012, -0.4229, -0.1539)]
        [InlineData(20.0, -0.1942, -0.0241, 0.0781, 0.4432)]
        public void AnArcOfJustOverAHalfCircle_PutsItsWordsOutsideTheOtherWayAndItsArcPointAThirdRound_AsAutoCad(
            double from,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 through 180.0001 degrees put 0.45 out, no arc symbol: the words run out level from the
            // first end, to the right from 140 degrees (a third of the way round is at 200, below the horizontal, where an arc
            // of 179.99 degrees sends them left) and to the left from 20 (60 above, where 179.99 sends them right), and the
            // arc point stands a third of the way round, not two thirds.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, from, 180.0001, 0.45);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Theory]
        [InlineData(180.0001, 1.0575, -0.1764, -0.2250, 0.3897)]
        [InlineData(180.001, -0.2295, -0.1764, 0.2250, 0.3897)]
        public void AnArcFromZeroDegrees_IsLaidOutAsUnderAHalfCircleUntilItIsAFewMillionthsOver_AsAutoCad(
            double sweep,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 from 0 through 180.0001 and 180.001 degrees put 0.45 out, no arc symbol: at 180.0001
            // (1.7 millionths of a radian over) the words stand to the right and the arc point two thirds round, as under 180
            // degrees; at 180.001 (17 millionths) to the left and a third round, as past it. From 10 degrees on, 180 degrees
            // itself already is past.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, 0.0, sweep, 0.45);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Theory]
        [InlineData(0.3, -0.3966, -0.1717, 0.1026, 0.2819)]
        [InlineData(0.4, -0.1035, 0.3864, 0.1368, 0.3759)]
        [InlineData(0.55, -0.1424, 0.5313, 0.1881, 0.5168)]
        public void AnArcOf210Degrees_KeepsItsWordsInOnceTheArcBeyondAHalfCircleIsTwoGapsLong_AsAutoCad(
            double distance,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 through 210 degrees from 0 put 0.3, 0.4 and 0.55 out, no arc symbol: the words
            // "3.6652" 0.75 wide, wider than the chord they would stand across, go outside at 0.3, but from 0.35 (the arc beyond
            // the half circle, 0.35 times 30 degrees, 0.18 long, is two gaps) stand in the middle of the arc, a third of the way
            // round.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, 0.0, 210.0, distance);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Theory]
        [InlineData(0.33, 0)]
        [InlineData(0.4, 2)]
        public void AnArcOf270Degrees_TakesBothArrowheadsInOnceTheArcBeyondAHalfCircleIsTwoGapsAndTwoArrowheadsLong_AsAutoCad(
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 through 270 degrees from 0, no arc symbol: the words (in the middle of the arc, level
            // at 135 degrees) and both arrowheads (0.18) came in from 0.35 out, where the arc beyond the half circle, 0.35
            // times 90 degrees, 0.55 long, is two gaps and two arrowheads; the words and the room they and the arrowheads
            // take between the rays had them in from 0.42.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, 0.0, 270.0, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 0.0, 270.0));
        }

        [Theory]
        [InlineData(75.0, true)]
        [InlineData(80.0, false)]
        public void LevelWordsByTheSecondRay_SendBothArrowheadsOutWhereTheirBreakReachesTheArrowheadRoom_AsAutoCad(
            double sweep,
            bool arcStays
        )
        {
            // AutoCAD 2021, the arc of 3 through 75 and 80 degrees from 10 put 1.8 out, DIMJUST 1, "4.1888" level with the
            // symbol before it: at 75 degrees the arc is broken about the words (59 to 79 degrees on the dimension's arc) and
            // the arrowheads stand in, at 80 the words' break would reach the second arrowhead's back and AutoCAD draws both
            // arrowheads outside, with no arc between them, as it does when they fit nowhere.
            CadDocument document = Styled(s => s.Dimjust = 1);
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, sweep, 1.8);

            double longest = dimension
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased && a.Radius > 1.0)
                .Max(a => Math.IEEERemainder(a.EndAngle - a.StartAngle - Math.PI, 2.0 * Math.PI) + Math.PI);

            Assert.Equal(arcStays, longest > 0.5);
        }

        [Fact]
        public void AlignedWordsByTheSecondRayOfAnArcPointingDown_ReadTheWayTheMiddleOfTheArcReads_AsAutoCad()
        {
            // AutoCAD 2021, the arc of 3 through 70 degrees from 135 put 1.8 out, DIMJUST 1, aligned "3.6652": their own
            // tangent by the second ray is at 276 degrees, which reads upright and which AutoCAD turns to 96 as it turns the
            // middle's tangent (260 degrees) to 80. The words stand at (-1.8012, -0.5274), turned 1.6773 radians.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = 1;
                s.Dimtih = false;
                s.Dimtoh = false;
            });
            DxfArcDimension dimension = Dimension(document, 3.0, 135.0, 70.0, 1.8);

            Near(Offset(-1.8012, -0.5274), Words(dimension), 2E-3);
            Assert.Equal(1.6773, dimension.DimBlock.OfType<DxfMText>().First().Rotation, 2E-3);
        }

        [Theory]
        [InlineData(1.12, 0)]
        [InlineData(1.17, 2)]
        public void LevelWordsWithTheSymbolOnAnArcOf100Degrees_NeedMoreRoomForBothArrowheads_AsAutoCad(
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 through 100 degrees from 10 put 1.12 and 1.17 out, DIMARCSYM 0: both arrowheads came
            // in at 1.14 (the words alone had them at 1.09), the first of a window of the arc's middle direction 56 to 83 degrees
            // (the arc's own is 60) for sweeps near 100 that wants 0.45 of the words' height more room.
            CadDocument document = Styled(s => s.Dimarcsym = 0);
            DxfArcDimension dimension = Dimension(document, 1.0, 10.0, 100.0, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsInside(dimension));
        }
    }
}
