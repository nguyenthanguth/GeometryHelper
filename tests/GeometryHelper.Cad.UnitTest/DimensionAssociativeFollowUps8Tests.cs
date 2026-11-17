using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Arc length dimensions (DIMARC) laid out as AutoCAD 2021 does where the last follow-ups left them: the side level words
    /// outside stand on for wide sweeps, the room the arc symbol takes on sweeps of 100 to 165 degrees, the words next to a
    /// ray (DIMJUST 1 and 2) laid by chords, and the arcs whose words and both arrowheads need more room. The numbers are
    /// those AutoCAD 2021's console gave for the same drawings, made with its own template (Standard in arial.ttf, text 0.18
    /// high, arrowheads 0.18, gap 0.09, words kept level by DIMTIH and DIMTOH, DIMDEC 4); positions are given from the
    /// arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps8Tests
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

        private static double Sweep(double chord) => Math.Round(2.0 * Math.Asin(chord / 20.0) * 180.0 / Math.PI, 4);

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

        [Theory]
        [InlineData(170.0, -1.0203, 0.2456, 0.1539, -0.4229)]
        [InlineData(350.0, 1.0203, -0.2456, -0.1539, 0.4229)]
        public void AnArcJustShortOfAHalfCircle_PutsItsWordsOutsideToTheSideItsArcPointLeans_AsAutoCad(
            double from,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 through 179.99 degrees put 0.45 out, no arc symbol: the words run out level from
            // the end they stand past to the left where a third of the way round the arc is below the horizontal, to the right
            // where it is above, not by where the first end points: from 170 degrees that end points up (to the right to
            // go by it) but the arc a third along has dropped below the horizontal, and the words stand to the left.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, from, 179.99, 0.45);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Theory]
        [InlineData(0.695, 1.6682, -0.0591, -0.2377, 0.6531)]
        [InlineData(0.7, 0.0610, 0.6973, 0.35, 0.6062)]
        public void TheArcSymbolBeforeWordsOnAWideSweep_TakesMoreRoomThanTheirBox_AsAutoCad(
            double distance,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 through 150 degrees from 10 put 0.695 and 0.7 out, DIMARCSYM 0: the words "2.6180"
            // and the symbol stand outside past the first end at 0.695 (the arc point two thirds round) and between the
            // rays at 0.7 (a third round). Their box and the chord and a third of the arc beyond it gave 0.68; the symbol takes
            // 1.3 times the square of the words' height more at 150 degrees, over the sine of the direction of the arc's middle.
            CadDocument document = Styled(s => s.Dimarcsym = 0);
            DxfArcDimension dimension = Dimension(document, 1.0, 10.0, 150.0, distance);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Fact]
        public void AlignedWordsNextToTheSecondRayOnAMovedInArc_StandAChordOfEachHalfFurtherAlong_AsAutoCad()
        {
            // AutoCAD 2021, the arc of 3 through 40 degrees from 10 put 2.4 out, DIMJUST 1, aligned words "2.0944" 0.75 wide
            // with the arc symbol before them: AutoCAD lays the arrowhead (0.18) and the half of the words and their symbol
            // (0.63) next to it as chords, so they stand 0.00125 further along than by arc (a cube of each over 24 radii
            // squared). The words stand at (2.0501, 1.2480) from the centre.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = 1;
                s.Dimtih = false;
                s.Dimtoh = false;
            });
            DxfArcDimension dimension = Dimension(document, 3.0, 10.0, 40.0, 2.4);

            Near(Offset(2.0501, 1.2480), Words(dimension), 4E-4);
        }

        [Theory]
        [InlineData(66.0, 2)]
        [InlineData(70.0, 0)]
        [InlineData(74.0, 0)]
        [InlineData(90.0, 0)]
        [InlineData(96.0, 2)]
        [InlineData(100.0, 2)]
        public void LevelWordsWithTheSymbolOnAnArcPointingNearlyUp_NeedMoreRoomForBothArrowheadsBesideThem_AsAutoCad(
            double from,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, arcs of 10 through a chord of 2.24 put 12 out, arrowheads 0.36: from 69 to 95 degrees (the middle
            // of the arc between 75 and 102) the chord that took both arrowheads between the rays with the words ran 0.12
            // later, 2.35, so they stand outside at 2.24; from 66 and 96 on they went in at 2.22.
            CadDocument document = Styled(s => s.Dimasz = 0.36);
            DxfArcDimension dimension = Dimension(document, 10.0, from, Sweep(2.24), 12.0);

            Assert.Equal(arrowheadsInside, ArrowheadsInside(dimension));
        }
    }
}
