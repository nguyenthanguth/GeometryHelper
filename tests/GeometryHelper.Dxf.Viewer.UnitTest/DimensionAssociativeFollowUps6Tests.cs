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
    /// The words and arrowheads of an arc length dimension (DIMARC) and of an angle of three points, laid out as AutoCAD 2021
    /// does where they only just fit: level words beside a ray under DIMJUST 1 and 2, aligned words above the arc, the
    /// arrowheads that win a room too small for both, the room between the arrowheads on a wide sweep, the symbol over the
    /// words. The numbers are those AutoCAD 2021's console gave for the same drawings, made with its own template (Standard in
    /// arial.ttf, text 0.18 high, arrowheads 0.18, gap 0.09, words kept level by DIMTIH and DIMTOH); positions are given from
    /// the arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps6Tests
    {
        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            set(document.Database.DimStyleTable["Standard"]);
            StandardCurrent(document.Database);
            return document;
        }

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static DxfDimension Dimension(
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
                sweep.ToString(CultureInfo.InvariantCulture)
            );
            double middle = from + sweep / 2.0;
            Run(session, "DIMARC", Polar(radius, middle), Polar(distance, middle));
            return (DxfDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
        }

        private static GeoPoint3 Offset(double dx, double dy) => new GeoPoint3(100 + dx, 100 + dy, 0);

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

        [Theory]
        [InlineData(1, 10.0, 40.0, 0, 3.0088, 1.9766)]
        [InlineData(2, 10.0, 60.0, 0, 3.1250, 1.7873)]
        [InlineData(1, 200.0, 30.0, 2, -2.9419, -2.0748)]
        [InlineData(2, 100.0, 50.0, 2, -1.6810, 3.1834)]
        public void LevelWordsBesideTheirRayUnderDimjust1And2_StandWhereTheirBoxTakesTheArc_AsAutoCad(
            int justify,
            double from,
            double sweep,
            int symbol,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, arcs of 3 put 3.6 out: the level words stand an arrowhead and a gap from their ray, then half of
            // what their box takes along the arc where it runs through its middle: their width and the symbol before them as
            // the sine of the direction of the arc's middle, their height as its cosine, each grown by a gap either side, a gap
            // at either end of the arc more: 1.12 for "2.0944" and the symbol at 30 degrees, where the fit before this took
            // 1.10 (the two joined at 45 degrees) and stood the words 0.013 too near their ray.
            CadDocument document = Styled(s =>
            {
                s.Dimjust = justify;
                s.Dimarcsym = symbol;
                s.Dimtad = 0;
            });
            DxfDimension dimension = Dimension(document, 3.0, from, sweep, 3.6);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Theory]
        [InlineData(0.18, 0.09, 11.6305, 3.6239)]
        [InlineData(0.36, 0.2, 11.6064, 4.3189)]
        public void AlignedWordsAboveTheArcBesideTheirRayUnderDimjust2_LeaveNoGapRoundThem_AsAutoCad(
            double textHeight,
            double gap,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, an arc of 10 through 40 degrees from 10 put 12 out, DIMTAD 1, the words turned along the arc: words
            // above the arc have no break to clear, so they stand an arrowhead and a gap from the ray, then half their width
            // and the symbol before them, with no gaps round them (words on the arc keep two either side).
            CadDocument document = Styled(s =>
            {
                s.Dimjust = 2;
                s.Dimtad = 1;
                s.Dimtih = false;
                s.Dimtoh = false;
                s.Dimtxt = textHeight;
                s.Dimgap = gap;
            });
            DxfDimension dimension = Dimension(document, 10.0, 10.0, 40.0, 12.0);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
        }

        [Theory]
        [InlineData(1.1, 8.8407, 8.1153, 2)]
        [InlineData(1.85, 7.664, 9.2338, 0)]
        [InlineData(1.95, 7.6175, 9.2722, 2)]
        public void AnArcWhoseWordsAndArrowheadsEachFitButNotTogether_KeepsTheLargerBetweenTheRays_AsAutoCad(
            double chord,
            double wordsX,
            double wordsY,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, arcs of 10 from 45 degrees put 12 out, DIMASZ 0.5 (1 for two arrowheads), DIMDEC 0, the words
            // turned: the words ("1" or "2", 0.757 or 0.79 with the symbol and the gaps) fit from a chord of 0.8 and the two
            // arrowheads from 1.0; where both fit, but not together, the arrowheads that take more stay between the rays (chords
            // of 1.0 to 1.8, the words outside), and from 1.85 (the room both need, and a gap round the words more) the words
            // stand between the rays with the arrowheads outside, until the chord of 1.95 takes both.
            CadDocument document = Styled(s =>
            {
                s.Dimasz = 0.5;
                s.Dimdec = 0;
                s.Dimtih = false;
                s.Dimtoh = false;
            });
            double sweep = Math.Round(2.0 * Math.Asin(chord / 20.0) * 180.0 / Math.PI, 4);
            DxfDimension dimension = Dimension(document, 10.0, 45.0, sweep, 12.0);

            Near(Offset(wordsX, wordsY), Words(dimension), 1E-3);
            Assert.Equal(arrowheadsInside, ArrowheadsInside(dimension));
        }

        [Theory]
        [InlineData(0.66, 0)]
        [InlineData(0.72, 2)]
        public void AnArcOfOneThroughAHundredAndThirtyFiveDegrees_FitsItsWordsBetweenTheArrowheadsFromTheRoomLeftOfTheSweep_AsAutoCad(
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 through 135 degrees from 10 dimensioned 0.66 and 0.72 from its centre, no symbol: the
            // words "2.3562" (1.17 with the gaps, level at 77.5 degrees) and both arrowheads fit from 0.695 out (the words alone from 0.58), where what is
            // left of the sweep once each arrowhead has taken its 15.5 degrees has a room (a chord and a third of what the arc
            // has beyond it) of 1.17; added to the arrowheads' 0.36 they would have needed 0.76.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfDimension dimension = Dimension(document, 1.0, 10.0, 135.0, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsInside(dimension));
        }

        [Fact]
        public void TheSymbolOverLevelWords_TakesTheArcOutAboveThemAsFarAsItStands_AsAutoCad()
        {
            // AutoCAD 2021, an arc of 10 through 5.85 degrees from 10 put 12 out, DIMARCSYM 1, DIMDEC 0 ("1"): the arc is taken
            // out from 0.184 before the words to 0.242 after them (the box of the words and the gap, up to 0.8 of their height
            // and a gap above them), the second piece of the arc standing from 0.2457.
            CadDocument document = Styled(s =>
            {
                s.Dimarcsym = 1;
                s.Dimdec = 0;
                s.Dimtad = 0;
            });
            double sweep = Math.Round(2.0 * Math.Asin(1.02 / 20.0) * 180.0 / Math.PI, 4);
            DxfDimension dimension = Dimension(document, 10.0, 10.0, sweep, 12.0);
            var arcs = dimension
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased && Math.Abs(a.Radius - 12.0) < 1E-6)
                .OrderBy(a => a.StartAngle)
                .ToList();

            Assert.Equal(2, arcs.Count);
            Assert.Equal(0.2102, arcs[0].EndAngle, 3);
            Assert.Equal(0.2457, arcs[1].StartAngle, 3);
        }

        [Theory]
        [InlineData(45.0, 10, 0.7554, 0.7704)]
        [InlineData(135.0, 10, 2.3262, 2.3412)]
        public void LevelWordsOfAnAngleOfThreePoints_FitWhereTheirBoxTakesTheArc_AsAutoCad(
            double from,
            int index,
            double firstStart,
            double firstEnd
        )
        {
            // AutoCAD 2021, rays 10 long from 45 and 135 degrees, 3.44 degrees apart (a chord of 0.6), the arc 12 out: the words
            // "3" (level, 0.55 wide with the gaps) do not fit between the rays at 3.9 and 1.8 degrees of direction, so the
            // arrowheads stay between them and the words stand outside.
            CadDocument document = Styled(s => { });
            double chord = 0.2 + 0.04 * index;
            double sweep = Math.Round(2.0 * Math.Asin(chord / 20.0) * 180.0 / Math.PI, 4);
            CadCommandSession session = Session(document);
            Run(
                session,
                "DIMANGULAR",
                Enter,
                P(100, 100),
                Polar(10.0, from),
                Polar(10.0, from + sweep),
                Polar(12.0, from + sweep / 2.0)
            );
            var dimension = (DxfDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
            var arcs = dimension
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased && Math.Abs(a.Radius - 12.0) < 1E-6)
                .OrderBy(a => a.StartAngle)
                .ToList();

            Assert.Equal(2, arcs.Count);
            Assert.Equal(firstStart, arcs[0].StartAngle, 3);
            Assert.Equal(firstEnd, arcs[0].EndAngle, 3);
        }
    }
}
