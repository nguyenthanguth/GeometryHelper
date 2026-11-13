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
    /// Words put near the extension lines of a linear dimension, the distance at which they get a leader, the arc of an
    /// angle clipped at the corner of its words, level words and the stubs of a jogged radius, and the width of letters Arial
    /// lacks, as AutoCAD 2021 draws them (accoreconsole: the dimensions made by DIMLINEAR, DIMANGULAR, DIMJOGGED and DIMTEDIT
    /// in it, or our file drawn again by it; every figure is its picture). The drawings are the metric template with a style
    /// of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial; a linear dimension of 100 along the x axis with its line 20
    /// up, whose words "100" are 5.28 wide, 6.53 with their gaps.
    /// </summary>
    public class DimensionFollowUps9Tests
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

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

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

        /// <summary>The lines of the dimension line itself (level, at <paramref name="y"/>), the extension lines not among them.</summary>
        private static List<DxfLine> LineAt(DxfDimension dimension, double y) =>
            Lines(dimension)
                .Where(l => Math.Abs(l.StartPoint.Y - y) < 1E-3 && Math.Abs(l.EndPoint.Y - y) < 1E-3)
                .ToList();

        private static DxfDimension Linear(CadCommandSession session, double wordsX, double wordsY = 20.0)
        {
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            DxfDimension linear = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { linear.Handle }, P(wordsX, wordsY));
            return linear;
        }

        // ---- words within an arrowhead and a gap and their width of an extension line ----

        [Theory]
        [InlineData(-5.0)]
        [InlineData(-3.0)]
        [InlineData(-0.5)]
        public void WordsPutBeforeTheFirstExtensionLineUnderDimtmove0WithinReachOfIt_AreLaidBesideItWithTheArrowheadsInside(
            double x
        )
        {
            // AutoCAD 2021: "100" put 3 before the first extension line of a line 100 long, its box 6.53 wide, would cover the
            // arrowhead's room: they are moved out to 5.765 before it, a line 2.5 long run back to them, the arrowheads inside.
            DxfDimension linear = Linear(Probe(v => v["Dimtad"] = 0), x);

            Near(-5.765, 20, Words(linear).Location);
            HasLine(linear, 0, 20, -2.5, 20);
            HasLine(linear, 2.5, 20, 97.5, 20);
            Assert.Equal(2, LineAt(linear, 20).Count);
        }

        [Fact]
        public void WordsPutOnTheFirstExtensionLineItselfUnderDimtmove0_StandPastTheSecondOne()
        {
            // AutoCAD 2021 reads a point exactly on the first extension line as past its end: the words stand 5.765 past the
            // second line, a line 2.5 long run on to them.
            DxfDimension linear = Linear(Probe(v => v["Dimtad"] = 0), 0);

            Near(105.765, 20, Words(linear).Location);
            HasLine(linear, 100, 20, 102.5, 20);
            HasLine(linear, 2.5, 20, 97.5, 20);
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(2.0)]
        [InlineData(3.0)]
        public void WordsPutJustInsideTheFirstExtensionLineUnderDimtmove0_TurnTheArrowheadsOutAndKeepTheLineWhole(
            double x
        )
        {
            // AutoCAD 2021: words on the line within the arrowhead's room of an extension line turn the arrowheads out, on lines
            // 2.5 long, and the dimension line runs whole from one extension line to the other under them (3 in: the gap the
            // line would be broken by would leave nothing before the words).
            DxfDimension linear = Linear(Probe(v => v["Dimtad"] = 0), x);

            Near(x, 20, Words(linear).Location);
            HasLine(linear, 0, 20, 100, 20);
            HasLine(linear, -5, 20, -2.5, 20);
            HasLine(linear, 102.5, 20, 105, 20);
            Assert.Equal(3, LineAt(linear, 20).Count);
        }

        [Fact]
        public void WordsPutOnTheLineBeyondTheReachOfTheArrowheadUnderDimtmove0_TurnTheArrowheadsOutAndBreakTheLine()
        {
            // AutoCAD 2021: 3.5 in the arrowheads are out still, and the line, now with room before the words, is broken a gap
            // clear of them: from 0 to 0.235 and from 6.765.
            DxfDimension linear = Linear(Probe(v => v["Dimtad"] = 0), 3.5);

            HasLine(linear, 0, 20, 0.235, 20);
            HasLine(linear, 6.765, 20, 100, 20);
            HasLine(linear, -5, 20, -2.5, 20);
            HasLine(linear, 102.5, 20, 105, 20);
        }

        [Fact]
        public void WordsPutAboveTheLineNearTheFirstExtensionLineUnderDimtmove0_LeaveTheArrowheadsInside()
        {
            // AutoCAD 2021: words above the line (DIMTAD 1) do not reach the arrowheads, so 2 in they stay inside, 1.901 above.
            DxfDimension linear = Linear(Probe(v => v["Dimtmove"] = 0), 2.0);

            Near(2, 21.901, Words(linear).Location);
            HasLine(linear, 2.5, 20, 97.5, 20);
            Assert.Single(LineAt(linear, 20));
        }

        [Fact]
        public void WordsPutBeforeTheFirstExtensionLineUnderDimtmove1_HaveNoLineRunBackAndNoBreakWithinReach()
        {
            // AutoCAD 2021: under DIMTMOVE 1 "100" put 3 before the first extension line stands there, the line whole between
            // the arrowheads and none run back to it, its box would be within an arrowhead's length of the line.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 0;
            });
            DxfDimension linear = Linear(session, -3.0);

            Near(-3, 20, Words(linear).Location);
            HasLine(linear, 2.5, 20, 97.5, 20);
            Assert.Single(LineAt(linear, 20));
        }

        [Fact]
        public void WordsPutBeforeTheFirstExtensionLineUnderDimtmove1_HaveTheLineRunBackOnceTheyClearTheArrowhead()
        {
            // AutoCAD 2021: 6 before the first extension line, clear of the arrowhead's length, the line runs back to the words.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 0;
            });
            DxfDimension linear = Linear(session, -6.0);

            HasLine(linear, 0, 20, -2.735, 20);
            HasLine(linear, 2.5, 20, 97.5, 20);
        }

        [Fact]
        public void WordsPutOnTheLineInsideTheFirstExtensionLineUnderDimtmove2_TurnTheArrowheadsOutAndKeepTheLineWhole()
        {
            // AutoCAD 2021: under DIMTMOVE 2 words 2 in from the first extension line, their middle at the point, turn the
            // arrowheads out and leave the line whole.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 2;
                v["Dimtad"] = 0;
            });
            DxfDimension linear = Linear(session, 2.0);

            Near(2, 20, Words(linear).Location);
            HasLine(linear, 0, 20, 100, 20);
            HasLine(linear, -5, 20, -2.5, 20);
            HasLine(linear, 102.5, 20, 105, 20);
        }

        // ---- the distance at which words put by DIMTEDIT under DIMTMOVE 1 get a leader ----

        [Theory]
        [InlineData(4.0, 4.5, false)]
        [InlineData(4.0, 4.75, true)]
        [InlineData(6.0, 6.5, false)]
        [InlineData(6.0, 6.75, true)]
        public void WordsPutAboveALineUnderDimtmove1_GetALeaderAtTheGapAndAnArrowheadOrTheirHalfHeightAndTheExtension(
            double arrow,
            double above,
            bool leader
        )
        {
            // AutoCAD 2021: the leader appears once the point is a gap and the larger of an arrowhead and half the words' height
            // with DIMEXE off the line (probed over arrowheads of 1.5 to 6, words 2.5 and 4 high, gaps of 0.3 to 1.5 and
            // DIMEXE 0 to 2.5): with arrowheads of 4 at 4.625, of 6 at 6.625; the arrowheads' length, not half of it.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 0;
                v["Dimasz"] = arrow;
            });
            DxfDimension linear = Linear(session, 30, 20 + above);

            bool drawn = Lines(linear)
                .Any(l =>
                    Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > 1E-3 && Math.Abs(l.StartPoint.X - l.EndPoint.X) > 1E-3
                );
            Assert.Equal(leader, drawn);
        }

        // ---- the arc of an angle clipped at the corner of its words ----

        [Fact]
        public void TheArcOfAnAngleWhoseWordsOnlyHaveTheirCornerClippedByIt_IsBrokenOnlyWhereItMeetsThem()
        {
            // AutoCAD 2021: "60°" put 4.75 off an arc of 30 at 30 degrees under DIMTMOVE 1 and a gap of 1.5 has the circle meet
            // the corner of its box between 29.1 and 29.4 degrees, 0.5082 and 0.5132 radians, not up to the words' angle.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimgap"] = 1.5;
            });
            Run(session, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension angle = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { angle.Handle }, Polar(30, 34.75));

            List<DxfArc> arcs = angle
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased)
                .OrderBy(a => a.StartAngle)
                .ToList();
            Assert.Equal(2, arcs.Count);
            Assert.True(Math.Abs(arcs[0].EndAngle - 0.5082) < 1E-3, $"the first arc ends at {arcs[0].EndAngle}");
            Assert.True(Math.Abs(arcs[1].StartAngle - 0.5132) < 1E-3, $"the second starts at {arcs[1].StartAngle}");
        }

        // ---- level words and the stub of a jogged radius ----

        private static DxfDimension Jogged(CadCommandSession session, double radius, double degrees, double factor)
        {
            Run(session, "C", P(0, 0), radius.ToString(CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            GeoPoint2 over = P(0, -100);
            GeoPoint2 location = Polar(degrees, radius * factor);
            Run(
                session,
                "DIMJOGGED",
                new[] { circle },
                over,
                location,
                P((over.X + location.X) / 2.0, (over.Y + location.Y) / 2.0)
            );
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        [Fact]
        public void AJoggedRadiusWhoseWordsReachPastTheBackOfItsArrowhead_HasTheLineDrawnFromTheBackToTheEndOfTheirBox()
        {
            // AutoCAD 2021: "R8" put 4 along a radius of 8 at 30 degrees under DIMATFIT 0 has its line broken a gap clear of
            // the words, and still drawn from the back of the arrowhead (5.5 out) to the box's far end (6.708), over it.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimjogang"] = Math.PI / 4.0;
            });
            DxfDimension jogged = Jogged(session, 8, 30, 0.5);

            Near(3.4641, 2.0, Words(jogged).Location);
            HasLine(jogged, 1.175, 0.678, -57.419, -33.151);
            HasLine(jogged, 4.763, 2.75, 5.753, 3.322);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void LevelWordsOfAJoggedRadius_StayLevelOnTheLineWhateverDimtadSays(int tad)
        {
            // AutoCAD 2021: under DIMTIH the words stay level and on the line, which is broken where it runs through their box
            // grown by the gap (a level box on a line at 30 degrees: 3.1 each side of its middle), and the stub from the back
            // of the arrowhead runs to the box's far end.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = tad;
                v["Dimtih"] = true;
                v["Dimtoh"] = true;
                v["Dimjogang"] = Math.PI / 4.0;
            });
            DxfDimension jogged = Jogged(session, 8, 30, 0.5);

            Near(3.4641, 2.0, Words(jogged).Location);
            Assert.True(Math.Abs(Words(jogged).Rotation) < 1E-3);
            HasLine(jogged, 0.821, 0.474, -57.419, -33.151);
            HasLine(jogged, 4.763, 2.75, 6.107, 3.526);
        }

        [Fact]
        public void LevelWordsOfAJoggedRadiusPutPastTheEnd_StandAGapAndTheirReachFromTheArrowhead()
        {
            // AutoCAD 2021: "R20" put 32 out at 120 degrees, past the jog's end, stands at 25.336 along the radius turned to the
            // jog: an arrowhead and a gap from the rim's back, and half the level box's chord (2.21) on from there.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtih"] = true;
                v["Dimtoh"] = true;
                v["Dimjogang"] = Math.PI / 4.0;
            });
            DxfDimension jogged = Jogged(session, 20, 120, 1.6);

            Near(-13.4366, -21.5158, Words(jogged).Location);
            HasLine(jogged, -14.624, -23.417, -31.826, -50.963);
            HasLine(jogged, -11.918, -19.084, -12.249, -19.614);
        }

        // ---- letters a font lacks ----

        [Theory]
        [InlineData("中", 2.2661)]
        [InlineData("∅", 1.9714)]
        public void WordsOfALetterArialLacks_AreSetAsHighAsAutoCADSetsThem(string word, double lift)
        {
            // AutoCAD 2021 draws the letters Arial has no glyph for from another font: U+4E2D is 2.7095 wide and 3.2821 high
            // there, U+2205 2.4454 and 2.6927, so that words above the line stand a gap and half that high (2.2661, 1.9714) up.
            CadCommandSession session = Probe(v => { });
            Run(session, "DLI", P(0, 0), P(100, 0), "Text", word, P(50, 20));
            DxfDimension linear = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

            Near(50, 20 + lift, Words(linear).Location);
        }
    }
}
