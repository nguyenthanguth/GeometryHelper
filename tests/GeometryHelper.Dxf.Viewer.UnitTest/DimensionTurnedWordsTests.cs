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
    /// Words of a linear dimension turned to an angle of their own (DIMTEDIT Angle) in the plain layout, where they do not fit
    /// between the extension lines: how much room they need, where they stand outside, and what the arrowheads do. Every
    /// figure is a picture made by accoreconsole from the file the library wrote (the dimension drawn again by
    /// RecomputeDimensionBlock; probes tw1 to tw16 of the turnwords round): the metric template with a style of arrowheads and
    /// words 2.5 high, DIMGAP 0.625 and Arial, "100" typed (5.2797 wide), a line at y = 30.
    /// </summary>
    public class DimensionTurnedWordsTests
    {
        private static CadCommandSession Probe(Action<SortedDictionary<string, object>> set)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "probe.dxf");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Probe", FileName = "arial.ttf" });
            string start = CadDimStyleEditor.Current(database);
            var values = new SortedDictionary<string, object>(CadDimStyleEditor.Read(database.DimStyleTable[start]))
            {
                ["Dimasz"] = 2.5,
                ["Dimtxt"] = 2.5,
                ["Dimgap"] = 0.625,
                ["Dimexo"] = 0.625,
                ["Dimexe"] = 1.25,
                ["Dimscale"] = 1.0,
                ["Dimdec"] = 0,
                ["Dimtad"] = 1,
                ["Dimtih"] = false,
                ["Dimtoh"] = false,
                ["Dimtofl"] = true,
                ["Dimatfit"] = 3,
                ["Dimcen"] = 2.5,
                ["Dimjust"] = 0,
                ["Dimtix"] = false,
                ["Dimsoxd"] = false,
                ["Dimtmove"] = 0,
                ["Dimtvp"] = 0.0,
                ["Dimadec"] = 0,
                ["Dimazin"] = 0,
                ["Dimtxsty"] = "Probe",
            };
            set(values);
            CadDimStyleEditor.Add(document, "Probe", start, values);
            CadDimStyleEditor.SetCurrent(document, "Probe");
            return Session(document);
        }

        /// <summary>The variables of the template's default style (0.18 high words and arrowheads, four decimals) at DIMSCALE 100.</summary>
        private static void DefaultStyleAtScale100(SortedDictionary<string, object> v)
        {
            v["Dimscale"] = 100.0;
            v["Dimasz"] = 0.18;
            v["Dimtxt"] = 0.18;
            v["Dimgap"] = 0.09;
            v["Dimexo"] = 0.0625;
            v["Dimexe"] = 0.18;
            v["Dimtad"] = 0;
            v["Dimtih"] = true;
            v["Dimtoh"] = true;
            v["Dimtofl"] = false;
            v["Dimdec"] = 4;
            v["Dimzin"] = 0;
            v["Dimdsep"] = '.';
        }

        /// <summary>A DIMLINEAR of the given length along x from the origin, its line 30 up, "100" typed, the words turned.</summary>
        private static DxfDimension Turned(
            CadCommandSession session,
            double length,
            double degrees,
            GeoPoint2? moved = null,
            string words = "100"
        )
        {
            Run(session, "DLI", P(0, 0), P(length, 0), "Text", words, P(length / 2.0, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

            if (moved.HasValue)
            {
                Run(session, "DIMTEDIT", new[] { dimension.Handle }, moved.Value);
            }

            Run(
                session,
                "DIMTEDIT",
                new[] { dimension.Handle },
                "A",
                degrees.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            return dimension;
        }

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static void Near(double x, double y, GeoPoint3 actual)
        {
            Assert.True(
                Math.Abs(actual.X - x) < 1E-3 && Math.Abs(actual.Y - y) < 1E-3,
                $"expected ({x}, {y}), got ({actual.X}, {actual.Y})"
            );
        }

        private static bool Joins(DxfLine l, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1E-3;
            bool forward =
                Math.Abs(l.StartPoint.X - x1) < tolerance
                && Math.Abs(l.StartPoint.Y - y1) < tolerance
                && Math.Abs(l.EndPoint.X - x2) < tolerance
                && Math.Abs(l.EndPoint.Y - y2) < tolerance;
            bool backward =
                Math.Abs(l.StartPoint.X - x2) < tolerance
                && Math.Abs(l.StartPoint.Y - y2) < tolerance
                && Math.Abs(l.EndPoint.X - x1) < tolerance
                && Math.Abs(l.EndPoint.Y - y1) < tolerance;
            return forward || backward;
        }

        private static void HasLine(DxfDimension dimension, double x1, double y1, double x2, double y2) =>
            Assert.Contains(Lines(dimension), l => Joins(l, x1, y1, x2, y2));

        private static void HasNoLine(DxfDimension dimension, double x1, double y1, double x2, double y2) =>
            Assert.DoesNotContain(Lines(dimension), l => Joins(l, x1, y1, x2, y2));

        /// <summary>Whether the first arrowhead (a solid at the first extension line) points out of the lines, its base inside.</summary>
        private static bool ArrowsInside(DxfDimension dimension)
        {
            DxfSolid first = dimension
                .DimBlock.OfType<DxfSolid>()
                .Where(s => !s.IsErased)
                .First(s => Enumerable.Range(0, 3).Any(i => Math.Abs(s.GetPointAt((short)i).X) < 1E-3));
            return Enumerable.Range(0, 3).Max(i => first.GetPointAt((short)i).X) > 1.0;
        }

        // ---- the room turned words need between the extension lines, and where they stand outside ----

        [Fact]
        public void WordsTurned30_OnALineOf7_DoNotFit_StandOutsideItsEndOnTheLine_AndTheArrowheadsStayIn()
        {
            // AutoCAD 2021: "100" (5.2797 wide, 2.5 high) turned 30 reaches 7.5549 along the line with its gaps (the width and the
            // height of its box over the cosine and the sine), more than 7: the words go out, centred on the line 2.5 (an arrowhead)
            // and 3.7699 (half the chord the line takes through them) past its end; the arrowheads need 6.25 and stay inside.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = Turned(session, 7, 30);

            Near(13.2699, 30, Words(dimension).Location);
            Assert.Equal(30.0 * Math.PI / 180.0, Words(dimension).Rotation, 3);
            HasLine(dimension, 2.5, 30, 4.5, 30);
            HasLine(dimension, 7, 30, 9.5, 30);
            Assert.True(ArrowsInside(dimension));
        }

        [Fact]
        public void WordsTurned30_OnALineOf8_FitBetweenTheLines_AndTheArrowheadsGoOut()
        {
            // AutoCAD 2021: 8 holds the box of the words (7.5549) but not the arrowheads with it (the chord 7.5398 and 6.25), so
            // under DIMATFIT 3 the words stay and the arrowheads go out, the line broken 3.7699 each side of the middle.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = Turned(session, 8, 30);

            Near(4, 30, Words(dimension).Location);
            HasLine(dimension, 0, 30, 0.2301, 30);
            HasLine(dimension, 7.7699, 30, 8, 30);
            HasLine(dimension, -2.5, 30, -5, 30);
            HasLine(dimension, 10.5, 30, 13, 30);
            Assert.False(ArrowsInside(dimension));
        }

        [Fact]
        public void WordsTurned60_OnALineOf6_GoOutByTheReachOfTheirBox_NotByTheShorterChord()
        {
            // AutoCAD 2021: turned 60 the chord the line takes through "100" is 4.39 but the box reaches 6.5549 along the line, so
            // on a line of 6 the words go out (a chord of 4.39 would have kept them in): 13.1956 = 6 + 5 + 2.1956, both arrowheads
            // out on lines 2.5 long.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = Turned(session, 6, 60);

            Near(13.1956, 30, Words(dimension).Location);
            HasLine(dimension, 0, 30, 6, 30);
            HasLine(dimension, 8.5, 30, 11, 30);
            HasLine(dimension, -2.5, 30, -5, 30);
        }

        [Fact]
        public void WordsTurned10_LongEnoughToNeedTheirReachAndTheArrowheads_FitOnlyBeyondBoth()
        {
            // AutoCAD 2021: "12345.6789" (17.9178 wide) turned 10 reaches 19.5 along the line: between 19.5 and 25.7 the words are
            // in and the arrowheads out; the arrowheads come in with them at 25.8 (the reach and the arrowheads' 6.25).
            CadCommandSession session = Probe(v => { });
            DxfDimension narrow = Turned(session, 25.7, 10, null, "12345.6789");
            session = Probe(v => { });
            DxfDimension wide = Turned(session, 25.8, 10, null, "12345.6789");

            Assert.False(ArrowsInside(narrow));
            Assert.True(ArrowsInside(wide));
            Near(12.85, 30, Words(narrow).Location);
            Near(12.9, 30, Words(wide).Location);
        }

        [Fact]
        public void WordsTurnedAndNotMoved_OnALineOf100AtDimscale100_StandOutsideWithTheArrowheadsIn()
        {
            // AutoCAD 2021 (the default style, 0.18 sizes at DIMSCALE 100, "100.0000" 100.94 wide and 18 high, DIMTAD 0): turned 30
            // the line takes a chord of 72.8 through the words, but their box reaches 121 along it, so they go out with the
            // arrowheads in, at 154.3806 and not between the lines.
            CadCommandSession session = Probe(DefaultStyleAtScale100);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, "A", "30");

            Near(154.3806, 30, Words(dimension).Location);
            HasLine(dimension, 18, 30, 82, 30);
            HasLine(dimension, 100, 30, 118, 30);

            Run(session, "DIMTEDIT", new[] { dimension.Handle }, "A", "20");

            Near(171.1849, 30, Words(dimension).Location);
        }

        [Fact]
        public void WordsMovedAndTurned30_OnALineOf100AtDimscale100_TurnTheArrowheadsOutWhereTheyReachThem()
        {
            // AutoCAD 2021, the same style and line, the words put at (20, 30) or (50, 30) and turned 30: they stand where they
            // were put and the arrowheads go out (18 long, on lines 18 long past 0 and 100); put at (120, 30), past the end,
            // the arrowheads stay in and the words stand 154.3806, clear of them.
            double[] puts = { 20, 50, 120 };

            for (int i = 0; i < puts.Length; i++)
            {
                CadCommandSession session = Probe(DefaultStyleAtScale100);
                Run(session, "DLI", P(0, 0), P(100, 0), P(50, 30));
                DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
                Run(session, "DIMTEDIT", new[] { dimension.Handle }, P(puts[i], 30));
                Run(session, "DIMTEDIT", new[] { dimension.Handle }, "A", "30");

                if (i < 2)
                {
                    Near(puts[i], 30, Words(dimension).Location);
                    HasLine(dimension, -18, 30, -36, 30);
                    HasLine(dimension, 118, 30, 136, 30);
                    Assert.False(ArrowsInside(dimension), "put at " + puts[i]);
                }
                else
                {
                    Near(154.3806, 30, Words(dimension).Location);
                    HasLine(dimension, 18, 30, 82, 30);
                    Assert.True(ArrowsInside(dimension), "put at " + puts[i]);
                }
            }
        }

        [Fact]
        public void WordsTurned30_MovedNearTheFirstLineOfALineOf14_TurnTheArrowheadsOutWhateverTheirHeight()
        {
            // AutoCAD 2021: "100" put at (1.4, 35) (5 above the line, 0.1 along it) and turned 30: the arrowheads go out, the line
            // moves to the words (y = 35) and stays whole.
            CadCommandSession session = Probe(v => { });
            DxfDimension near = Turned(session, 14, 30, P(1.4, 35));

            Near(1.4, 35, Words(near).Location);
            HasLine(near, 0, 35, 14, 35);
            HasLine(near, -2.5, 35, -5, 35);
            HasLine(near, 16.5, 35, 19, 35);
        }

        // ---- over the line (DIMTMOVE 1 and 2), over an extension line (DIMJUST 3), lifted (DIMTVP), on a slanting line ----

        [Fact]
        public void WordsTurned30_OutsideUnderDimtmove1_StandOnALevelLandingTurnedAboutTheMiddleOfTheirStart()
        {
            // AutoCAD 2021: a line of 4, "100" turned 30, DIMTMOVE 1: the leader runs square up from the middle of the line to
            // DIMEXE, half DIMGAP and the height of the words (4.1154), the landing level 6.53 long (the width and a gap each side)
            // from there, the words held by the middle of their start (2.625, 34.1154) and turned about it.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 4, 30);

            Near(4.9112, 35.4353, Words(dimension).Location);
            HasLine(dimension, 2, 30, 2, 34.1154);
            HasLine(dimension, 2, 34.1154, 8.5297, 34.1154);
        }

        [Fact]
        public void WordsTurned30_OutsideUnderDimtmove2_StandAboveTheLineByTheLevelClearance()
        {
            // AutoCAD 2021: DIMTMOVE 2 puts the middle of the words 4.1154 above the middle of the line, turned or not.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 2);
            DxfDimension dimension = Turned(session, 4, 30);

            Near(2, 34.1154, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned30_UnderDimtmove1_MovedAndTurned_StandOnTheirPointWithALeaderAndTheLandingBehindIt()
        {
            // AutoCAD 2021: a line of 14, "100" put at (7, 38) and turned 30 under DIMTMOVE 1: the leader to a gap short of the
            // point, the landing 6.53 on from there, the words turned about the point and not shifted along the line.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 14, 30, P(7, 38));

            Near(9.2862, 39.3199, Words(dimension).Location);
            HasLine(dimension, 7, 30, 6.375, 38);
            HasLine(dimension, 6.375, 38, 12.9047, 38);
        }

        [Fact]
        public void VeryWideWordsTurned60_OnALineOf5_UnderDimtmove1_StandCentredOnTheirStartWithNoLeader()
        {
            // AutoCAD 2021 (annot5, probe tw10): "1234567890" (18.876 wide) turned 60 on a line of 5 under DIMTMOVE 1: half the
            // height of their turned box, (9.438 + 0.625) sin 60 + (1.277 + 0.625) cos 60 = 9.67, is more than twice the 4.117
            // the leader would rise (8.23), so AutoCAD draws no leader and no landing and centres the words on the point
            // where their start would be (3.125, 34.117): a gap past the middle of the line.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 5, 60, null, "1234567890");

            Assert.True(Math.Abs(Words(dimension).Location.X - 3.125) < 2E-3, $"x {Words(dimension).Location.X}");
            Assert.True(Math.Abs(Words(dimension).Location.Y - 34.117) < 2E-3, $"y {Words(dimension).Location.Y}");
            Assert.DoesNotContain(Lines(dimension), l => Math.Abs(l.StartPoint.X - 2.5) < 1E-3 && l.StartPoint.Y > 31);
            Assert.DoesNotContain(Lines(dimension), l => Math.Abs(l.EndPoint.X - 2.5) < 1E-3 && l.EndPoint.Y > 31);
        }

        [Fact]
        public void WordsTurned60_OnALineOf5_UnderDimtmove1_JustNarrowerThanThatKeepTheirLeader()
        {
            // AutoCAD 2021 (annot5, probe tw10): "12345678" (15.005 wide) turned 60 has half its box 7.99 high, under the 8.23:
            // the leader (2.5, 30) to (2.5, 34.117), the landing 16.255 long, the words (6.876, 40.615) held by their start.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 5, 60, null, "12345678");

            Assert.True(Math.Abs(Words(dimension).Location.X - 6.876) < 2E-3, $"x {Words(dimension).Location.X}");
            Assert.True(Math.Abs(Words(dimension).Location.Y - 40.615) < 2E-3, $"y {Words(dimension).Location.Y}");
            Assert.Contains(Lines(dimension), l => Joins(l, 2.5, 30, 2.5, 34.117));
            Assert.Contains(Lines(dimension), l => Joins(l, 2.5, 34.117, 18.755, 34.117));
        }

        [Theory]
        [InlineData(13, true)]
        [InlineData(14, false)]
        public void WordsTurned30_OnALineOf5_UnderDimtmove1_LoseTheirLeaderBetween13And14Digits(int digits, bool leader)
        {
            // AutoCAD 2021 (annot5, probe tw10): turned 30, half the box (13 digits 8.14, 14 digits 8.62) against 8.23.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 5, 30, null, "1234567890123456".Substring(0, digits));

            Assert.Equal(leader, Lines(dimension).Any(l => Joins(l, 2.5, 30, 2.5, 34.117)));
        }

        [Fact]
        public void WordsTurned120_OnALineOf5_UnderDimtmove1_NineDigitsStandCentredOnTheirStart()
        {
            // AutoCAD 2021 (annot5, probe tw10): the box is as high turned 120 as turned 60, and the words stand on (3.125, 34.117).
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = Turned(session, 5, 120, null, "123456789");

            Assert.True(Math.Abs(Words(dimension).Location.X - 3.125) < 2E-3, $"x {Words(dimension).Location.X}");
            Assert.True(Math.Abs(Words(dimension).Location.Y - 34.117) < 2E-3, $"y {Words(dimension).Location.Y}");
        }

        [Fact]
        public void WordsTurned30_UnderDimjust3_StandCentredOnTheExtensionLineAndItIsNotRunOn()
        {
            // AutoCAD 2021: over the first extension line the words stand on it, DIMEXE, a gap and half their width up
            // (34.5148 - 30 = 4.5148 above the line), not beside it, and the extension line ends where it always does.
            CadCommandSession session = Probe(v => v["Dimjust"] = 3);
            DxfDimension dimension = Turned(session, 40, 30);

            Near(0, 34.5148, Words(dimension).Location);
            HasLine(dimension, 0, 0.625, 0, 31.25);
        }

        [Fact]
        public void WordsTurned30_UnderDimtvp_AreLiftedOffTheLine_ByDimtvpTimesTheirHeightUnderDimtad0()
        {
            // AutoCAD 2021: DIMTAD 0 and DIMTVP 0.5 lift "100" by half its height (1.2764), the line broken about where it stood.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtad"] = 0;
                v["Dimtvp"] = 0.5;
            });
            DxfDimension dimension = Turned(session, 40, 30);

            Near(20, 31.2764, Words(dimension).Location);
            HasLine(dimension, 2.5, 30, 16.2301, 30);
            HasLine(dimension, 37.5, 30, 23.7699, 30);
        }

        [Fact]
        public void WordsTurned30_UnderDimtvpAndDimtad1_StandClearOfTheLineByTheReachOfTheirTurnedBox()
        {
            // AutoCAD 2021: any DIMTVP but nought lifts turned words under DIMTAD 1 as well: they stand clear of the line by the
            // half height of their box turned (3.2791 for "100" turned 30), and by a gap and half their height (1.9015) where
            // they go over the line.
            CadCommandSession session = Probe(v => v["Dimtvp"] = 0.5);
            DxfDimension dimension = Turned(session, 40, 30);

            Near(20, 33.2791, Words(dimension).Location);
            HasLine(dimension, 2.5, 30, 16.2301, 30);
        }

        [Fact]
        public void WordsTurned60_OnAnAlignedLineOf4At30Degrees_StandLevelWithTheEndOfItsLastArrowhead()
        {
            // AutoCAD 2021: a DIMALIGNED of 4 along 30 degrees with its line 30 off, "100" turned 60: the words stand centred
            // half the chord level on from where the line ends (-7.2058, 30.4808), not along the line.
            CadCommandSession session = Probe(v => { });
            GeoPoint2 along = new GeoPoint2(Math.Cos(Math.PI / 6.0), Math.Sin(Math.PI / 6.0));
            Run(
                session,
                "DAL",
                P(0, 0),
                P(4 * along.X, 4 * along.Y),
                "Text",
                "100",
                P(2 * along.X - 30 * along.Y, 2 * along.Y + 30 * along.X)
            );
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, "A", "60");

            Near(-3.4359, 30.4808, Words(dimension).Location);
            HasLine(dimension, -9.3708, 29.2308, -7.2058, 30.4808);
        }
    }
}
