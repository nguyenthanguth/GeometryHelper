using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
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

        /// <summary>
        /// A DIMALIGNED of the given length from the origin along <paramref name="slope"/> degrees, its line 30 off, the words
        /// put <paramref name="along"/> (of its length) and <paramref name="across"/> (past the line) from it and turned to
        /// <paramref name="angle"/>. The first coordinate of the frame is along the line, the second square to it on its left.
        /// </summary>
        private static DxfDimension MovedAndTurnedOnASlope(
            CadCommandSession session,
            double slope,
            double length,
            double along,
            double across,
            double angle
        )
        {
            GeoPoint2 at(double a, double n) => Frame(slope, a, n);
            Run(session, "DAL", P(0, 0), at(length, 0), "Text", "100", at(length / 2.0, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, at(length * along, 30 + across));
            Run(
                session,
                "DIMTEDIT",
                new[] { dimension.Handle },
                "A",
                angle.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            return dimension;
        }

        private static GeoPoint2 Frame(double slope, double a, double n)
        {
            double c = Math.Cos(slope * Math.PI / 180.0);
            double s = Math.Sin(slope * Math.PI / 180.0);
            return P(a * c - n * s, a * s + n * c);
        }

        private static void HasFramedLine(
            DxfDimension dimension,
            double slope,
            double a1,
            double n1,
            double a2,
            double n2
        )
        {
            GeoPoint2 from = Frame(slope, a1, n1);
            GeoPoint2 to = Frame(slope, a2, n2);
            HasLine(dimension, from.X, from.Y, to.X, to.Y);
        }

        private static void HasNoFramedLine(
            DxfDimension dimension,
            double slope,
            double a1,
            double n1,
            double a2,
            double n2
        )
        {
            GeoPoint2 from = Frame(slope, a1, n1);
            GeoPoint2 to = Frame(slope, a2, n2);
            HasNoLine(dimension, from.X, from.Y, to.X, to.Y);
        }

        // ---- words put past the end of a slanting line under DIMTMOVE 0 and turned (tw17, tw18 of the annot6 round) ----

        [Fact]
        public void WordsTurned20_PutPastTheEndOfAnUprightLineOf40UnderDimtmove0_MoveTheLineOutByTheirChord()
        {
            // AutoCAD 2021: "100" turned 20 on a DIMALIGNED of 40 along 90 degrees, put 8 past its end and 5 over the line: the
            // words stay at their point, but the whole dimension line moves 2.0235 further out, the chord the line takes through
            // their box (the line took the chord along itself, then AutoCAD lays it level), and runs on to the level of the point.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 90, 40, 1.2, 5, 20);

            Near(-35, 48, Words(dimension).Location);
            Assert.Equal(20.0 * Math.PI / 180.0, Words(dimension).Rotation, 3);
            HasFramedLine(dimension, 90, 2.5, 37.0235, 37.5, 37.0235);
            HasFramedLine(dimension, 90, 40, 37.0235, 48, 37.0235);
            HasFramedLine(dimension, 90, 0, 0.625, 0, 38.2735);
            HasFramedLine(dimension, 90, 40, 0.625, 40, 38.2735);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At30DegreesUnderDimtmove0_StopTheLineShortByTheLevelChord()
        {
            // AutoCAD 2021: the chord of 3.3153 is laid along x, not along the line: the line moves out by 3.3153 sin 30 = 1.6577
            // and ends 3.3153 cos 30 = 2.8711 short of the words' level (45.1289 for words at 48).
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 40, 1.2, 5, 20);

            Near(Frame(30, 48, 35).X, Frame(30, 48, 35).Y, Words(dimension).Location);
            HasFramedLine(dimension, 30, 2.5, 36.6577, 37.5, 36.6577);
            HasFramedLine(dimension, 30, 40, 36.6577, 45.1289, 36.6577);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf7At30DegreesUnderDimtmove0_StandAnArrowheadAndTheLevelChordOnFromTheLine()
        {
            // AutoCAD 2021: the words, put 1.44 past the end of a line of 7.2, are moved out to 2.5 and 3.3153 cos 30 past it
            // (12.5711), the line moved out 1.6577 and run on 2.5 past its end.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 7.2, 1.2, 5, 20);

            Near(Frame(30, 12.5711, 35).X, Frame(30, 12.5711, 35).Y, Words(dimension).Location);
            HasFramedLine(dimension, 30, 7.2, 36.6577, 9.7, 36.6577);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At120DegreesUnderDimtmove0_MoveTheLineTheOtherWayOut()
        {
            // AutoCAD 2021: the words lie to the left of the end of this line, so the level chord (1.9306, "100" turned 20 against
            // a line at 120 degrees) takes the line the other way: 1.672 back in, ending 0.9653 short of the level of the words.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 120, 40, 1.2, 5, 20);

            Near(Frame(120, 48, 35).X, Frame(120, 48, 35).Y, Words(dimension).Location);
            HasFramedLine(dimension, 120, 2.5, 33.328, 37.5, 33.328);
            HasFramedLine(dimension, 120, 40, 33.328, 47.035, 33.328);
        }

        [Fact]
        public void WordsTurned60_PutBeforeTheStartOfADownwardLineOf40UnderDimtmove0_MoveTheLineInByTheChord()
        {
            // AutoCAD 2021: a DIMALIGNED of 40 along 270 degrees, "100" turned 60 put 8 before its start: the line moves 3.77 the
            // way the words lie (towards them on the left of the line) and runs on to their level.
            CadCommandSession session = Probe(v => { });
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 270, 40, -0.2, 5, 60);

            Near(Frame(270, -8, 35).X, Frame(270, -8, 35).Y, Words(dimension).Location);
            HasFramedLine(dimension, 270, 2.5, 31.23, 37.5, 31.23);
            HasFramedLine(dimension, 270, 0, 31.23, -8, 31.23);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfAnUprightLineOf40UnderDimtmove1_HaveTheLineRunOnToTheirLevelAndALandingOfAnArrowhead()
        {
            // AutoCAD 2021 (probes tw17, tw18, tw20, tw21): the line is run on past its end to the level of the point (8 on), a
            // level landing runs back from there towards the words but is never shorter than an arrowhead (2.5), the words
            // starting at its end: the box of "100" (3.2649 each side) would leave 1.7351 of the 5, so they move out by 0.765.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 90, 40, 1.2, 5, 20);

            HasFramedLine(dimension, 90, 40, 30, 48, 30);
            HasFramedLine(dimension, 90, 48, 30, 48, 32.5);
            Near(Frame(90, 48, 35.765).X, Frame(90, 48, 35.765).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At30DegreesUnderDimtmove1_StandOnTheirPointAtTheEndOfTheLandingBack()
        {
            // AutoCAD 2021: the line run on 16.66 to the level of the point, the landing 6.735 back to a gap past the words (their
            // half width, 3.2649, from their point), which stay on their point.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 40, 1.2, 5, 20);

            HasFramedLine(dimension, 30, 40, 30, 56.66, 30);
            HasFramedLine(dimension, 30, 56.66, 30, 50.827, 33.368);
            Near(Frame(30, 48, 35).X, Frame(30, 48, 35).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutBeforeTheStartOfALineOf40At315DegreesUnderDimtmove1_HaveTheLineRunOnBackwards()
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 315, 40, -0.2, 5, 20);

            HasFramedLine(dimension, 315, 0, 30, -13, 30);
            HasFramedLine(dimension, 315, -13, 30, -10.309, 32.691);
            Near(Frame(315, -8, 35).X, Frame(315, -8, 35).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At20DegreesUnderDimtmove1_KeepTheLeaderFromTheMiddleOfTheLine()
        {
            // AutoCAD 2021: a line of 40 at 20 degrees with the words 5 over it keeps the leader from its middle, as the line run
            // on is the longer leader from 3.566 across (28 degrees and more run it on up to 5 across; probe tw20, 480 cases).
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 20, 40, 1.2, 5, 20);

            HasFramedLine(dimension, 20, 20, 30, 47.413, 35.214);
            HasFramedLine(dimension, 20, 47.413, 35.214, 53.549, 32.98);
            Near(Frame(20, 50.64, 35).X, Frame(20, 50.64, 35).Y, Words(dimension).Location);
        }

        // ---- where AutoCAD goes back from the line run on to the leader from the middle (probes tw20, tw22 to tw29 of the annot7 round) ----

        /// <summary>A DIMALIGNED of the given length along <paramref name="slope"/>, "100" put <paramref name="past"/> past its end (before its start when negative) and <paramref name="across"/> over the line, turned 20, DIMTMOVE 1.</summary>
        private static DxfDimension PutPast(double slope, double length, double past, double across)
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            return MovedAndTurnedOnASlope(session, slope, length, (length + past) / length, across, 20);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At60Degrees_AreRunOnTo13AcrossAndLedFromTheMiddleBeyond()
        {
            // AutoCAD 2021 (tw22, tw23 and tw28 down to 0.0025): the line run on to the level of the words (their point 8 past the
            // end) is the shorter leader, the run and the landing together (15.662 + 15.325 = 30.987 at 13.2713 across), than the
            // straight one from the middle (30.987), up to 13.2713 across; "100" 13 over is run on, 14 over has the leader.
            DxfDimension run = PutPast(60, 40, 8, 13);

            HasFramedLine(run, 60, 40, 30, 55.506, 30);
            HasFramedLine(run, 60, 55.506, 30, 49.632, 40.173);
            Near(Frame(60, 48, 43).X, Frame(60, 48, 43).Y, Words(run).Location);

            DxfDimension led = PutPast(60, 40, 8, 14);

            HasFramedLine(led, 60, 20, 30, 47.687, 44.541);
            HasFramedLine(led, 60, 47.687, 44.541, 50.952, 38.886);
            HasNoFramedLine(led, 60, 40, 30, 55.506, 30);
            Near(Frame(60, 50.022, 42.303).X, Frame(60, 50.022, 42.303).Y, Words(led).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf25At45Degrees_AreRunOnTo5AcrossAndLedFromTheMiddleFrom6()
        {
            // AutoCAD 2021 (tw22): the shorter leader again, the least across 5.5 to 6 for a line of 25 at 45 degrees (40 long: 8 to 9).
            DxfDimension run = PutPast(45, 25, 8, 5);

            HasFramedLine(run, 45, 25, 30, 38, 30);
            HasFramedLine(run, 45, 38, 30, 35.309, 32.691);
            Near(Frame(45, 33, 35).X, Frame(45, 33, 35).Y, Words(run).Location);

            DxfDimension led = PutPast(45, 25, 8, 6);

            HasFramedLine(led, 45, 12.5, 30, 32.558, 36.442);
            HasFramedLine(led, 45, 32.558, 36.442, 37.175, 31.825);
            Near(Frame(45, 35.392, 34.884).X, Frame(45, 35.392, 34.884).Y, Words(led).Location);
        }

        [Fact]
        public void WordsTurned20_PutPast5OfALineOf40At60Degrees_AreRunOnToTheirLevelToo()
        {
            // AutoCAD 2021 (tw24, 5 to 30 past): the cut-off hardly moves with how far past the end the words are put (13.52 across
            // at 5 past, 12.42 at 30); words within an arrowhead and their half chord of the end are run on as those further out.
            DxfDimension dimension = PutPast(60, 40, 5, 13.5);

            HasFramedLine(dimension, 60, 40, 30, 52.794, 30);
            HasFramedLine(dimension, 60, 52.794, 30, 46.632, 40.673);
            Near(Frame(60, 45, 43.5).X, Frame(60, 45, 43.5).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At20Degrees_AreRunOnWithinAGapAndAnArrowheadOfItAndLedFromTheMiddleBeyondTheLength()
        {
            // AutoCAD 2021 (tw23, tw29): over the line by less than DIMGAP and an arrowhead (3.125) the line is run on whatever its
            // length; from there the shorter leader decides, and a line of 40 at 20 degrees is run on up to 3.566 across (3.5 yes,
            // 4 no), where 28 degrees and more had it up to 5.
            DxfDimension near = PutPast(20, 40, 8, 3);

            HasFramedLine(near, 20, 40, 30, 56.242, 30);
            HasFramedLine(near, 20, 56.242, 30, 51.068, 31.883);
            Near(Frame(20, 48, 33).X, Frame(20, 48, 33).Y, Words(near).Location);

            DxfDimension run = PutPast(20, 40, 8, 3.5);

            HasFramedLine(run, 20, 40, 30, 57.616, 30);
            HasFramedLine(run, 20, 57.616, 30, 51.068, 32.383);

            DxfDimension led = PutPast(20, 40, 8, 4);

            HasFramedLine(led, 20, 20, 30, 47.413, 34.214);
            HasFramedLine(led, 20, 47.413, 34.214, 53.549, 31.98);
            Near(Frame(20, 50.64, 34).X, Frame(20, 50.64, 34).Y, Words(led).Location);
        }

        [Fact]
        public void WordsTurned20_OverAShortLineAt20Degrees_AreRunOnToTheirLevelAndALandingOfAnArrowhead()
        {
            // AutoCAD 2021 (tw23): "100" 1 over a line of 10 at 20 degrees, 8 past its end: the line run on 10.747 to their level,
            // the landing the least of 2.5 (the box would leave 2.92 - 3.265), the words moved in to stand at its end.
            DxfDimension dimension = PutPast(20, 10, 8, 1);

            HasFramedLine(dimension, 20, 10, 30, 20.747, 30);
            HasFramedLine(dimension, 20, 20.747, 30, 18.398, 30.855);
            Near(Frame(20, 15.33, 31.972).X, Frame(20, 15.33, 31.972).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf40At140Degrees_HaveTheRunMadeAnArrowheadLongAndTheWordsMoveWithIt()
        {
            // AutoCAD 2021 (tw20, slopes 136 to 146): the run to the level of the words is 2.04, less than an arrowhead; the line is
            // run on 2.5, and the landing (4.514) and the words move with its end, 0.459 further along, the words 0.295 higher.
            DxfDimension dimension = PutPast(140, 40, 8, 5);

            HasFramedLine(dimension, 140, 40, 30, 42.5, 30);
            HasFramedLine(dimension, 140, 42.5, 30, 45.958, 32.901);
            Near(Frame(140, 48.459, 35).X, Frame(140, 48.459, 35).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfAnUprightLineOf7UnderDimtmove1_HaveTheLineRunOnAnArrowheadAndALandingOfOne()
        {
            // AutoCAD 2021 (tw17, tw18): 1.44 past the end of an upright line of 7.2 and 5 over it, the level of the words is 1.44 on
            // but the line is run on 2.5, a landing of 2.5 from there, and the words stand 1.06 higher than where they were put.
            DxfDimension dimension = PutPast(90, 7.2, 1.44, 5);

            HasFramedLine(dimension, 90, 7.2, 30, 9.7, 30);
            HasFramedLine(dimension, 90, 9.7, 30, 9.7, 32.5);
            Near(Frame(90, 9.7, 35.765).X, Frame(90, 9.7, 35.765).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutOverTheStartOfALineOf40At135Degrees_RunTheLineOnBackwardsToTheirLevel()
        {
            // AutoCAD 2021 (tw18): the words 4 along from the start of the line and 5 over it at 135 degrees lie below the level of
            // its start, so the line is run on backwards from there (2.5, the run of 1 made an arrowhead long) and a level
            // landing runs back to the words, which move 1.5 along.
            DxfDimension dimension = PutPast(135, 40, -36, 5);

            HasFramedLine(dimension, 135, 0, 30, -2.5, 30);
            HasFramedLine(dimension, 135, -2.5, 30, 0.191, 32.691);
            Near(Frame(135, 2.5, 35).X, Frame(135, 2.5, 35).Y, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutOverALevelLineWithinTheLeadersReach_StandOnTheLineWithItRunOnToAGapShortOfThem()
        {
            // AutoCAD 2021 (tw29, tw31: 54 of 54): a level line has no level to run on to, so the words put past an end and 2 over
            // the line of 40 are laid on the line (centred at (48, 30), not (48, 32)) and the line is run on 4.735 to a gap and half
            // their width (3.265) short of them; the same before the start and on a line at 180 degrees.
            DxfDimension dimension = PutPast(0, 40, 8, 2);

            HasFramedLine(dimension, 0, 40, 30, 44.735, 30);
            Near(48, 30, Words(dimension).Location);

            DxfDimension before = PutPast(0, 40, -48, 2);

            HasFramedLine(before, 0, -4.735, 30, 0, 30);
            Near(-8, 30, Words(before).Location);

            DxfDimension back = PutPast(180, 7.2, 8, 2);

            HasFramedLine(back, 180, 7.2, 30, 11.935, 30);
            Near(Frame(180, 15.2, 30).X, Frame(180, 15.2, 30).Y, Words(back).Location);
        }

        [Fact]
        public void WordsTurned20_PutPastTheEndOfALineOf5UnderDimtmove1_RunTheLineOnAtLeastTwoArrowheadsAndStartItAtTheEndOfTheirLine()
        {
            // AutoCAD 2021 (tw30, 72 probes of lines of 3, 5 and 6): the arrowheads of a line of 5 stand out on a line of their own, an
            // arrowhead long and past one, so the run on to the level of the words (5.113 at 120 degrees with the words 8 past and 5
            // over) starts where that line ends, and a run shorter than two arrowheads is made that long (150 degrees, 3.5 over:
            // the landing and the words move out 2.5 with its end, the run being 2.5 short of it).
            DxfDimension run = PutPast(120, 5, 8, 5);

            HasFramedLine(run, 120, 10, 30, 10.113, 30);
            HasFramedLine(run, 120, 10.113, 30, 11.368, 32.173);
            HasNoFramedLine(run, 120, 5, 30, 10.113, 30);
            Near(Frame(120, 13, 35).X, Frame(120, 13, 35).Y, Words(run).Location);

            DxfDimension shifted = PutPast(150, 5, 8, 3.5);

            HasFramedLine(shifted, 150, 10, 30, 13.235, 31.868);
            Near(Frame(150, 16.062, 33.5).X, Frame(150, 16.062, 33.5).Y, Words(shifted).Location);
        }

        [Theory]
        [InlineData(45, 5, 2)]
        [InlineData(120, 5, 1)]
        [InlineData(150, 3.5, 1)]
        [InlineData(90, 3.5, 1)]
        public void WordsTurned20_PutPastTheEndOfALineOf6UnderDimtmove1_DrawItTwiceOnlyWithTheLeaderFromTheMiddle(
            double slope,
            double across,
            int copies
        )
        {
            // AutoCAD 2021 (tw30): the line between the extension lines of a short slanting line (arrowheads out) is drawn twice where
            // the leader goes from its middle (58 of 72 probes) and once where it is run on to the words (14 of 72).
            DxfDimension dimension = PutPast(slope, 6, 8, across);
            GeoPoint2 from = Frame(slope, 0, 30);
            GeoPoint2 to = Frame(slope, 6, 30);

            Assert.Equal(copies, Lines(dimension).Count(l => Joins(l, from.X, from.Y, to.X, to.Y)));
        }

        [Theory]
        [InlineData(120, 13, true)]
        [InlineData(120, 14, false)]
        [InlineData(135, 7, true)]
        [InlineData(135, 8, false)]
        public void WordsTurned20_PutPastTheEndOfALineOf25PastAnUprightOne_AreRunOnWhileTheirLevelIsBeyondTheEnd(
            double slope,
            double across,
            bool runOn
        )
        {
            // AutoCAD 2021 (tw22): on a line leaning back (120 and 135 degrees) the run to the level of the words is the shorter
            // leader as long as it is more than nothing (0.49 and 1 at 13 and 7 across, 0 at 14 and 8 across): nothing beyond that.
            DxfDimension dimension = PutPast(slope, 25, 8, across);
            GeoPoint2 middle = Frame(slope, 12.5, 30);

            // The leader from the middle is there where the line is not run on, and only then.
            Assert.Equal(
                !runOn,
                Lines(dimension)
                    .Any(l =>
                        (Math.Abs(l.StartPoint.X - middle.X) < 1E-3 && Math.Abs(l.StartPoint.Y - middle.Y) < 1E-3)
                        || (Math.Abs(l.EndPoint.X - middle.X) < 1E-3 && Math.Abs(l.EndPoint.Y - middle.Y) < 1E-3)
                    )
            );
        }

        /// <summary>A DIMALIGNED of the given length from the origin along <paramref name="slope"/> degrees, its line 30 off, under DIMTOH and DIMTIH, DIMTMOVE 1.</summary>
        private static DxfDimension LevelWordsOnASlope(double slope, double length, string words, int tad = 1)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
                v["Dimtad"] = tad;
            });
            Run(session, "DAL", P(0, 0), Frame(slope, length, 0), "Text", words, Frame(slope, length / 2.0, 30));
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        [Fact]
        public void LevelWords_TooHighForTheLine_StandCentredOnTheirStartWithNoLeader()
        {
            // AutoCAD 2021 (probe tw19): level "1234567890" (DIMTOH) on a DIMALIGNED of 9 at 45 degrees under DIMTMOVE 1: the
            // half of their box across the line, (W / 2 + gap) sin t + (H / 2 + gap) cos t, reaches twice the leader's rise, so
            // there is no leader and the words stand centred where they would start, as turned words do.
            DxfDimension dimension = LevelWordsOnASlope(45, 9, "1234567890");

            Near(Frame(45, 5.4032, 35.9041).X, Frame(45, 5.4032, 35.9041).Y, Words(dimension).Location);
            GeoPoint2 from = Frame(45, 4.5, 30);
            GeoPoint2 to = Frame(45, 4.5, 34.12);
            Assert.DoesNotContain(Lines(dimension), l => Joins(l, from.X, from.Y, to.X, to.Y));
        }

        [Fact]
        public void LevelWords_OnASlantingLineThatTheirBoxReachesAcross_GoOutByTheReachNotTheChord()
        {
            // AutoCAD 2021 (probe tw19): level "123456789" on a DIMALIGNED of 9 at 30 degrees reaches 10.3 along the line with
            // its gaps (the chord the line takes through the box is only 3.8, which would have kept it in): the words go out
            // on a leader square to the line from its middle, the landing level.
            DxfDimension dimension = LevelWordsOnASlope(30, 9, "123456789");

            HasFramedLine(dimension, 30, 4.5, 30, 4.5, 34.117);
            HasFramedLine(dimension, 30, 4.5, 34.117, -11.2595, 43.2158);
            Near(Frame(30, -2.4286, 40.3139).X, Frame(30, -2.4286, 40.3139).Y, Words(dimension).Location);
        }

        /// <summary>The copies of the line between the extension lines that a dimension draws: from the first point's offset to the second's.</summary>
        private static int CopiesOfTheLine(DxfDimension dimension, double slope, double length)
        {
            GeoPoint2 from = Frame(slope, 0, 30);
            GeoPoint2 to = Frame(slope, length, 30);
            return Lines(dimension).Count(l => Joins(l, from.X, from.Y, to.X, to.Y));
        }

        [Theory]
        [InlineData(30, 5, 14, -36.734, 30.981, -6.340, -22.787, 32.883)]
        [InlineData(45, 5, 10, -36.768, 28.284, -14.142, -26.705, 30.187)]
        [InlineData(120, 5, 15, -55.846, -19.330, -23.481, -40.913, -17.428)]
        public void LevelWordsTooHighForALineWithItsArrowheadsOut_RunItOnToTheLevelOfTheKnee_AndStandOnALevelLandingFromThere(
            double slope,
            double length,
            int digits,
            double landX,
            double landY,
            double bendX,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021 (probe tw19, 30 of its 64 cases that differed; tw32, 1 152 cases): the leader's knee is 4.117 over the middle
            // of the line (the words' height, DIMEXE and half a gap); the line, run on, reaches that level run_k = 4.117 cot s - l / 2
            // beyond the nearer end (4.63 for a line of 5 at 30 degrees, 1.62 at 45 degrees, 1.35 at 120 degrees). The bend is at
            // least two arrowheads out (5 for 2.5), the end of the short line the arrowhead stands on, so nothing more is drawn
            // to it; a level landing leaves from there towards the knee's side, an arrowhead and the words and their gaps long
            // (W + 3.74 for "12345678901234", W 26.66), the words starting an arrowhead and a gap along it, and the line between
            // the extension lines is drawn once, not twice as it is with the leader from the middle.
            DxfDimension dimension = LevelWordsOnASlope(slope, length, "1234567890123456".Substring(0, digits));

            HasLine(dimension, landX, landY, bendX, landY);
            Near(wordsX, wordsY, Words(dimension).Location);
            Assert.Equal(1, CopiesOfTheLine(dimension, slope, length));
        }

        [Theory]
        [InlineData(30, 2, -8.938, 29.481, -7.958, 30.046, -57.776, -34.117, 31.948)]
        [InlineData(30, 12, -4.608, 31.981, -2.443, 33.231, -52.260, -28.601, 35.133)]
        public void LevelWordsTooHighForALine_RunMoreThanTheArrowheadsNeedOrLessThanOne_StartTheLandingAtTheEndOfTheRun(
            double slope,
            double length,
            double runFromX,
            double runFromY,
            double bendX,
            double bendY,
            double landingToX,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021 (probe tw32): 24 digits on a line of 2 at 30 degrees: the run of 6.13 to the knee's level (4.117 cot 30
            // less 1) goes on from the end of the short line of the arrowhead (7 on) to 8.131; on a line of 12, which has its
            // arrowheads in, the run (1.13) is made an arrowhead long, 14.5, and starts at the line's end.
            DxfDimension dimension = LevelWordsOnASlope(slope, length, "123456789012345678901234");

            HasLine(dimension, runFromX, runFromY, bendX, bendY);
            HasLine(dimension, landingToX, bendY, bendX, bendY);
            Near(wordsX, wordsY, Words(dimension).Location);
        }

        [Theory]
        [InlineData(30, 5, 60, 1, -8.840, -6.340, 30.981, -24.733)]
        [InlineData(30, 5, 60, 0, -8.840, -6.340, 30.981, -24.733)]
        [InlineData(45, 5, 20, 1, -16.642, -14.142, 28.284, -32.536)]
        [InlineData(120, 3, 20, 1, -25.981, -23.481, -19.330, -41.874)]
        public void TurnedWordsTooHighForASlantingLine_PutOutByTheFit_RunItOnToTheLevelOfTheKnee_AndStandAtTheEndOfAnArrowheadLongLanding(
            double slope,
            double length,
            double angle,
            int tad,
            double landFromX,
            double landToX,
            double landY,
            double wordsX
        )
        {
            // AutoCAD 2021 (probe tw33, 432 pictures, 116 of which differed): the words typed as 16 digits, put out by the fit and then
            // turned (DIMTEDIT Angle), reach too high across the line for a leader as level ones do, and the line is run on to the knee's
            // level in the same way (two arrowheads at least where the arrowheads stand on a short line of their own); the landing
            // is an arrowhead long whatever DIMTAD says, and the words stand level with it, centred a gap and half their width
            // past its end, turned to their angle.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = tad;
            });
            Run(
                session,
                "DAL",
                P(0, 0),
                Frame(slope, length, 0),
                "Text",
                "1234567890123456",
                Frame(slope, length / 2.0, 30)
            );
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(
                session,
                "DIMTEDIT",
                new[] { dimension.Handle },
                "A",
                angle.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

            HasLine(dimension, landFromX, landY, landToX, landY);
            Near(wordsX, landY, Words(dimension).Location);
        }

        [Fact]
        public void LevelWordsTooHighForALineWithItsArrowheadsIn_RunItOnToTheLevelOfTheKnee()
        {
            // AutoCAD 2021 (probe tw19): 14 digits on a line of 9 at 30 degrees: the run of 2.63 (7.13 less 4.5) from the end of the
            // line to the knee's level, the landing 30.394 long from there to a gap past the words.
            DxfDimension dimension = LevelWordsOnASlope(30, 9, "12345678901234");

            HasLine(dimension, -7.206, 30.481, -4.927, 31.796);
            HasLine(dimension, -35.321, 31.796, -4.927, 31.796);
            Near(-21.374, 33.698, Words(dimension).Location);
        }

        [Fact]
        public void LevelWordsTooHighForALine_UnderDimtad0_LandAnArrowheadLongAndStandOnTheLevel()
        {
            // AutoCAD 2021 (probe tw19, DIMTAD 0): the landing is an arrowhead long (2.5), the words start an arrowhead and a gap
            // past the bend and stand level with it.
            DxfDimension dimension = LevelWordsOnASlope(30, 5, "12345678901234", 0);

            HasLine(dimension, -8.840, 30.981, -6.340, 30.981);
            Near(-22.787, 30.981, Words(dimension).Location);
        }

        [Fact]
        public void LevelWordsTooHighForALine_ThatNeverReachesTheKneesLevel_DrawTheLineTwiceAndGoNoFurther()
        {
            // AutoCAD 2021 (probe tw19): 9 digits on a line of 5 at 60 degrees: the knee's level lies 0.12 short of the line's end
            // (4.117 cot 60 is 2.377, the half of the line 2.5), so there is nothing to run on to: the words stand centred where
            // they would start, and the arrowheads standing out the line between the extension lines is drawn twice (once at 45
            // degrees, where a run is made).
            DxfDimension dimension = LevelWordsOnASlope(60, 5, "123456789");

            Near(-28.921, 21.126, Words(dimension).Location);
            Assert.Equal(2, CopiesOfTheLine(dimension, 60, 5));
        }

        [Theory]
        [InlineData(0.5, -1, 16.861, 23.492, 2.820, 35.115)]
        [InlineData(0.5, 1, 16.509, 23.139, 1.821, 36.847)]
        [InlineData(0.5, 2.25, 19.049, 25.680, 1.196, 37.929)]
        public void WordsTurned20_PutWithinTheLeadersReachOfALineOf40At30Degrees_BreakItAboutTheMiddleOfTheChordTheirBoxTakes(
            double along,
            double across,
            double fromX,
            double toX,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021 (probes tw27, tw34; 3 510 and 738 cases): under DIMTMOVE 1 words put 3.125 or less over or under the line
            // stand where they were put and break the line by 6.63, but about the middle of what the line takes of their box (half
            // 3.275 along them, half 1.9 across, the gaps in), not about the foot of their point: 0.18 on for words 1 under it,
            // 0.18 back for 1 over it, 2.37 on for 2.25 over it, where the box only grazes it.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 40, along, across, 20);
            GeoPoint2 start = Frame(30, 2.5, 30);
            GeoPoint2 end = Frame(30, 37.5, 30);

            HasLine(dimension, start.X, start.Y, Frame(30, fromX, 30).X, Frame(30, fromX, 30).Y);
            HasLine(dimension, end.X, end.Y, Frame(30, toX, 30).X, Frame(30, toX, 30).Y);
            Near(wordsX, wordsY, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutBetweenTheExtensionLinesBelowTheLevelOfTheStartOfALine_RunTheLineOnBackwardsToTheirLevel()
        {
            // AutoCAD 2021 (probe tw35, 2 028 cases, and tw34): the run to the level of the point is not for words past an end only:
            // "100" turned 20 put 2 along a line of 40 at 30 degrees and 3 under it lies 2.6 below the level of its start, so the line
            // is run on backwards 3.196 to that level (the leader from the middle would be 20), and a level landing of 2.735 comes back
            // from there to a gap short of the words, which stay on their point.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 40, 0.05, -3, 20);

            HasFramedLine(dimension, 30, 0, 30, -3.196, 30);
            HasLine(dimension, -15.033, 24.383, -17.768, 24.383);
            Near(-11.768, 24.383, Words(dimension).Location);
        }

        [Fact]
        public void WordsTurned20_PutBetweenTheExtensionLinesJustBelowTheLevelOfTheStart_TurnTheArrowheadsOutAndRunTheLineOnTwoArrowheads()
        {
            // AutoCAD 2021 (probe tw35): "100" turned 20 put 1.5 along a line of 40 at 30 degrees and 1 under it lies 0.116 below the
            // level of its start: the arrowheads turn out, the run is made the two arrowheads of the short line they stand on (5), the
            // landing starts there and the words move to its level (2.384 lower than their point), and the line between the
            // extension lines is drawn once, not twice.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, 30, 40, 0.0375, -1, 20);

            HasLine(dimension, -16.830, 23.481, -19.330, 23.481);
            Near(-13.565, 23.481, Words(dimension).Location);
            Assert.Equal(1, CopiesOfTheLine(dimension, 30, 40));
        }

        [Fact]
        public void WordsTurned20_PutOver2OfALineOf40At30DegreesWhereTheirBreakWouldTakeAnArrowhead_TurnTheArrowheadsOutAndDrawTheLineTwice()
        {
            // AutoCAD 2021 (probe tw27): "100" turned 20, put 2.25 over the line 2 along it: the break (6.63 wide, centred 2.37
            // on) would take the first arrowhead, so the arrowheads stand out and the line is whole, drawn twice; put 4 along it
            // the break starts at 3.05, clear of the arrowhead (2.5), and is made with the arrowheads in.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension taken = MovedAndTurnedOnASlope(session, 30, 40, 0.05, 2.25, 20);

            Assert.Equal(2, CopiesOfTheLine(taken, 30, 40));
            HasFramedLine(taken, 30, 42.5, 30, 45, 30);
            Near(-14.393, 28.929, Words(taken).Location);

            session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension clear = MovedAndTurnedOnASlope(session, 30, 40, 0.1, 2.25, 20);

            Assert.Equal(0, CopiesOfTheLine(clear, 30, 40));
            HasFramedLine(clear, 30, 2.5, 30, 3.049, 30);
        }

        [Theory]
        [InlineData(30, 20, 2)]
        [InlineData(30, 90, 2)]
        [InlineData(200, 60, 2)]
        [InlineData(15, 20, 2)]
        [InlineData(0, 20, 1)]
        [InlineData(180, 90, 1)]
        [InlineData(30, 60, 0)]
        public void WordsTurnedOnTheMiddleOfALineOf7UnderDimtmove1_TurnTheArrowheadsOut_AndASlantingLineIsDrawnTwice(
            double slope,
            double angle,
            int copies
        )
        {
            // AutoCAD 2021: "100" turned on the middle of a DIMALIGNED of 7.2 under DIMTMOVE 1 turns the arrowheads out where its
            // turned box reaches them, and then draws the line between the extension lines twice on top of itself, unless the
            // line is level (probes tw17 and tw18: 29 cases of slanting lines, the level ones once); where the arrowheads stay in
            // (30 degrees with "100" turned 60) the line is drawn between them and not whole.
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            DxfDimension dimension = MovedAndTurnedOnASlope(session, slope, 7.2, 0.5, 0, angle);
            GeoPoint2 from = Frame(slope, 0, 30);
            GeoPoint2 to = Frame(slope, 7.2, 30);

            Assert.Equal(copies, Lines(dimension).Count(l => Joins(l, from.X, from.Y, to.X, to.Y)));
        }
    }
}
