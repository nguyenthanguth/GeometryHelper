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
    /// The second follow-ups of MLINE against the records AutoCAD 2021 left (accoreconsole, <c>Wblock</c> of the multilines
    /// into a side database written by <c>DxfOut</c>): the open and merged tees on a closed multiline, EXTEND to oblique
    /// edges, trims near a corner and the weld across the first vertex of a closed multiline. Unless said, the style STANDARD
    /// at scale 20 justified at the top, so that the second element stands 20 to the right of the line; the square of 200
    /// is drawn counter-clockwise from (0, 0).
    /// </summary>
    public class MlineFollowUps2Tests
    {
        private const double Close = 1E-9;

        private static readonly double[] Whole = { 0.0 };
        private static readonly double[] None = new double[0];

        private static CadCommandSession Start(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        private static List<DxfUnknownEntity> Mlines(CadDocument document) =>
            All<DxfUnknownEntity>(document.ModelSpace).Where(m => m.DxfName == "MLINE").ToList();

        /// <summary>A drawing of multilines of STANDARD at scale 20 (C last for a closed one) and lines (L first).</summary>
        private static CadDocument Drawing(params object[][] shapes)
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            foreach (object[] shape in shapes)
            {
                if (shape[0] is string kind && kind == "L")
                {
                    Run(session, "LINE", shape[1], shape[2], Enter);
                    continue;
                }

                Run(session, "MLINE");
                Run(session, shape);

                if (!(shape[shape.Length - 1] is string))
                {
                    Run(session, Enter);
                }
            }

            return document;
        }

        /// <summary>A drawing whose current style FOUR has the elements 1, 0.5, -0.5 and -1, at scale 10 justified at the top (0) or zero (1), as the probe made it.</summary>
        private static CadDocument Four(string justification, params object[][] lines)
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Run(session, "MLSTYLE", Enter);
            var settings = new DxfMlineStyleSettings();
            settings.Elements.Clear();

            foreach (double offset in new[] { 1.0, 0.5, -0.5, -1.0 })
            {
                settings.Elements.Add(new DxfMlineStyleElement(offset, DxfColor.ByLayer, "BYLAYER"));
            }

            document.Database.AddMlineStyle("FOUR", settings);
            Run(session, "CMLSTYLE", "FOUR", "CMLSCALE", "10", "CMLJUST", justification);

            foreach (object[] line in lines)
            {
                Run(session, "MLINE");
                Run(session, line);

                if (!(line[line.Length - 1] is string))
                {
                    Run(session, Enter);
                }
            }

            return document;
        }

        private static object[] Ml(params double[] xy)
        {
            var points = new List<object>();

            for (int i = 0; i < xy.Length; i += 2)
            {
                points.Add(P(xy[i], xy[i + 1]));
            }

            return points.ToArray();
        }

        private static object[] Closed(params double[] xy) => Ml(xy).Concat(new object[] { "C" }).ToArray();

        private static object[] L(double x1, double y1, double x2, double y2) =>
            new object[] { "L", P(x1, y1), P(x2, y2) };

        private static readonly object[] Square = Closed(0, 0, 200, 0, 200, 200, 0, 200);

        private static CadCommandSession Edit(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Start(document);
            Run(session, inputs);
            return session;
        }

        /// <summary>The parameters after the offset that an element holds at a vertex, as the record writes them.</summary>
        private static double[] Tail(DxfUnknownEntity mline, int vertex, int element)
        {
            int at = -1;
            int each = -1;
            var found = new List<double>();

            foreach (DxfTypedValue group in mline.Groups)
            {
                if (group.TypeCode == 11)
                {
                    at++;
                    each = -1;
                }
                else if (group.TypeCode == 74)
                {
                    each++;
                }
                else if (group.TypeCode == 41 && at == vertex && each == element)
                {
                    found.Add(double.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
                }
            }

            return found.Skip(1).ToArray();
        }

        private static void Tails(DxfUnknownEntity mline, int vertex, params double[][] expected)
        {
            for (int e = 0; e < expected.Length; e++)
            {
                double[] actual = Tail(mline, vertex, e);
                Assert.True(
                    expected[e].Length == actual.Length
                        && expected[e].Zip(actual, (a, b) => Math.Abs(a - b) < Close).All(same => same),
                    "vertex "
                        + vertex
                        + " element "
                        + e
                        + ": AutoCAD wrote ["
                        + string.Join(", ", expected[e])
                        + "], ours is ["
                        + string.Join(", ", actual)
                        + "] all: "
                        + string.Join(
                            " | ",
                            Enumerable.Range(0, expected.Length).Select(k => string.Join(",", Tail(mline, vertex, k)))
                        )
                );
            }
        }

        private static void Points(DxfUnknownEntity mline, params GeoPoint2[] expected)
        {
            IList<GeoPoint3> points = new DxfMlineBuilder(mline).Points;
            Assert.Equal(expected.Length, points.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(new GeoPoint3(expected[i].X, expected[i].Y, 0), points[i], 1E-9);
            }
        }

        [Theory]
        [InlineData("OT")]
        [InlineData("MT")]
        public void ATeeOnTheBottomOfAClosedMline_FromInsideIt_BreaksItsNearElementAcrossTheStem(string option)
        {
            // AutoCAD 2021: the stem (100, 100)-(100, -50) picked at (100, 50) onto the first segment of the square.
            CadDocument document = Drawing(Square, Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", option, P(100, 50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, new[] { 0.0, 80.0, 100.0 }, Whole);
            Tails(mlines[0], 1, Whole, Whole);
            Tails(mlines[0], 2, Whole, Whole);
            Tails(mlines[0], 3, Whole, Whole);
            Points(mlines[1], P(100, 100), P(100, 0));
            Tails(mlines[1], 0, Whole, Whole);
            Tails(mlines[1], 1, None, None);
            Assert.True(new DxfMlineBuilder(mlines[1]).SuppressEndCaps);
        }

        [Theory]
        [InlineData("OT")]
        [InlineData("MT")]
        public void ATeeOnTheBottomOfAClosedMline_FromOutsideIt_BreaksTheFarElementAcrossTheStem(string option)
        {
            // AutoCAD 2021: the stem (100, -100)-(100, 50) picked at (100, -50): its near element is the one 20 below the line.
            CadDocument document = Drawing(Square, Ml(100, -100, 100, 50));

            Edit(document, "MLEDIT", option, P(100, -50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, Whole, new[] { 0.0, 120.0, 140.0 });
            Tails(mlines[0], 1, Whole, Whole);
            Points(mlines[1], P(100, -100), P(100, 0));
            Tails(mlines[1], 0, new[] { 0.0, 80.0 }, new[] { 0.0, 80.0 });
            Tails(mlines[1], 1, None, None);
        }

        [Theory]
        [InlineData("OT")]
        [InlineData("MT")]
        public void ATeeOnTheClosingSegmentOfAClosedMline_BreaksItsElementAcrossTheStem(string option)
        {
            // AutoCAD 2021: the stem (100, 100)-(-50, 100) picked at (50, 100) onto the last segment of the square, (0, 200)-(0, 0).
            CadDocument document = Drawing(Square, Ml(100, 100, -50, 100));

            Edit(document, "MLEDIT", option, P(50, 100), P(0, 150), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 3, new[] { 0.0, 80.0, 100.0 }, Whole);
            Tails(mlines[0], 0, Whole, Whole);
            Points(mlines[1], P(100, 100), P(0, 100));
            Tails(mlines[1], 0, Whole, Whole);
            Tails(mlines[1], 1, None, None);
        }

        [Fact]
        public void AnOpenTeeOfFourElementsOnAClosedMline_StopsTheStemAtTheNearElement_AndBreaksOnlyThatElement()
        {
            // AutoCAD 2021, FOUR at scale 10 justified at the top: the square, the stem (100, 100)-(100, -50).
            CadDocument document = Four("0", Square, Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", "OT", P(100, 50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, new[] { 0.0, 80.0, 100.0 }, Whole, Whole, Whole);
            Points(mlines[1], P(100, 100), P(100, 0));
            Tails(mlines[1], 0, Whole, Whole, Whole, Whole);
            Tails(mlines[1], 1, None, None, None, None);
        }

        [Fact]
        public void AMergedTeeOfFourElementsOnAClosedMline_RunsTheInnerPairOnAndBreaksTheBarsPairAcross()
        {
            CadDocument document = Four("0", Square, Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", "MT", P(100, 50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, new[] { 0.0, 80.0, 100.0 }, new[] { 0.0, 90.0, 100.0 }, Whole, Whole);
            Tails(mlines[1], 0, Whole, Whole, Whole, Whole);
            Tails(mlines[1], 1, None, new[] { 0.0, 5.0 }, new[] { 0.0, 5.0 }, None);
        }

        [Fact]
        public void AnOpenTeeOfFourElementsFromOutsideAClosedMline_StopsEveryElementAtTheNearOne()
        {
            CadDocument document = Four("0", Square, Ml(100, -100, 100, 50));

            Edit(document, "MLEDIT", "OT", P(100, -50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, Whole, Whole, Whole, new[] { 0.0, 120.0, 140.0 });
            double[] stop = { 0.0, 80.0 };
            Tails(mlines[1], 0, stop, stop, stop, stop);
            Tails(mlines[1], 1, None, None, None, None);
        }

        [Fact]
        public void AMergedTeeOfFourElementsFromOutsideAClosedMline_StopsTheInnerPairAtTheBarsInnerPair()
        {
            CadDocument document = Four("0", Square, Ml(100, -100, 100, 50));

            Edit(document, "MLEDIT", "MT", P(100, -50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, Whole, Whole, new[] { 0.0, 120.0, 130.0 }, new[] { 0.0, 120.0, 140.0 });
            Tails(mlines[1], 0, new[] { 0.0, 80.0 }, new[] { 0.0, 85.0 }, new[] { 0.0, 85.0 }, new[] { 0.0, 80.0 });
            Tails(mlines[1], 1, None, None, None, None);
        }

        [Fact]
        public void AnOpenTeeOfFourElementsAtAnAngleOnAClosedMline_StopsTheElementsWhereTheyMeetTheNearElement()
        {
            // AutoCAD 2021: the stem (60, 100)-(140, -50) ends where its first element meets the bar's, at (113.33, 0).
            CadDocument document = Four("0", Square, Ml(60, 100, 140, -50));

            Edit(document, "MLEDIT", "OT", P(81.33333333333333, 60), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, new[] { 0.0, 90.666666666666686, 113.3333333333333 }, Whole, Whole, Whole);
            Points(mlines[1], P(60, 100), P(113.3333333333333, 0));
            Tails(
                mlines[1],
                0,
                Whole,
                new[] { 0.0, 110.6666666666666 },
                new[] { 0.0, 105.3333333333333 },
                new[] { 0.0, 102.6666666666667 }
            );
            Tails(mlines[1], 1, None, None, None, None);
        }

        [Fact]
        public void AMergedTeeOfFourElementsAtAnAngleOnAClosedMline_RunsOnlyTheElementThatMeetsTheBarsInnerOne()
        {
            CadDocument document = Four("0", Square, Ml(60, 100, 140, -50));

            Edit(document, "MLEDIT", "MT", P(81.33333333333333, 60), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(
                mlines[0],
                0,
                new[] { 0.0, 90.666666666666686, 113.3333333333333 },
                new[] { 0.0, 104.0, 115.3333333333333 },
                Whole,
                Whole
            );
            Tails(mlines[1], 0, Whole, Whole, new[] { 0.0, 111.0 }, new[] { 0.0, 102.6666666666667 });
            Tails(mlines[1], 1, None, new[] { 0.0, 3.00000000000001 }, None, None);
        }

        [Fact]
        public void AnOpenTeeKeepingTheEndBeyondAClosedMline_StartsTheStemAtItsLineAndItsElementsAtTheNearOne()
        {
            // AutoCAD 2021: the stem (100, 100)-(100, -50) picked at (100, -30), past the bar: kept from (100, 0), its elements
            // starting 20 on, where the bar's lowest element stands.
            CadDocument document = Four("0", Square, Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", "OT", P(100, -30), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, Whole, Whole, Whole, new[] { 0.0, 100.0, 120.0 });
            Points(mlines[1], P(100, 0), P(100, -50));
            Tails(mlines[1], 0, new[] { 20.0 }, new[] { 20.0 }, new[] { 20.0 }, new[] { 20.0 });
            Assert.True(new DxfMlineBuilder(mlines[1]).SuppressStartCaps);
        }

        [Fact]
        public void AMergedTeeKeepingTheEndBeyondAClosedMline_StartsTheInnerPairAtTheBarsInnerPair()
        {
            CadDocument document = Four("0", Square, Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", "MT", P(100, -30), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, Whole, Whole, new[] { 0.0, 100.0, 110.0 }, new[] { 0.0, 100.0, 120.0 });
            Tails(mlines[1], 0, new[] { 20.0 }, new[] { 15.0 }, new[] { 15.0 }, new[] { 20.0 });
        }

        [Theory]
        [InlineData("OT", false)]
        [InlineData("MT", true)]
        public void ATeeAtTheEdgeOfAClosedMline_BreaksTheNearElementsWhereTheStemStands(string option, bool merged)
        {
            // AutoCAD 2021: the stem (195, 100)-(195, -50), whose elements stand 195, 190, 180 and 175.
            CadDocument document = Four("0", Square, Ml(195, 100, 195, -50));

            Edit(document, "MLEDIT", option, P(195, 50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(
                mlines[0],
                0,
                new[] { 0.0, 175.0, 195.0 },
                merged ? new[] { 0.0, 185.0, 195.0 } : Whole,
                Whole,
                Whole
            );
            Tails(mlines[1], 1, None, merged ? new[] { 0.0, 5.0 } : None, merged ? new[] { 0.0, 5.0 } : None, None);
        }

        private const string SelfIntersecting = "Invalid operation on a self intersecting multiline.";

        [Theory]
        [InlineData("OT")]
        [InlineData("CT")]
        public void ATeeWhoseElementsMeetTheClosedMlinePastItsCorner_StopsOnlyTheElementsThatMeetIt_AndLeavesTheBarWhole(
            string option
        )
        {
            // AutoCAD 2021, FOUR at scale 10: the stem (12, 100)-(12, -50), whose elements stand 12, 7, -3 and -8, ends at
            // (12, 0); the first two stop at the bar's element, the last two, which meet it before its start, run on.
            CadDocument document = Four("0", Square, Ml(12, 100, 12, -50));

            CadCommandSession session = Edit(document, "MLEDIT", option, P(12, 50), P(150, 0));

            Assert.Contains(SelfIntersecting, session.History);
            List<DxfUnknownEntity> mlines = Mlines(document);
            Points(mlines[1], P(12, 100), P(12, 0));
            Tails(mlines[1], 0, Whole, Whole, Whole, Whole);
            Tails(mlines[1], 1, None, None, Whole, Whole);

            for (int v = 0; v < 4; v++)
            {
                Tails(mlines[0], v, Whole, Whole, Whole, Whole);
            }
        }

        [Fact]
        public void AnOpenTeeWhoseLastElementMeetsTheClosedMlinePastItsCorner_StopsTheOthers()
        {
            // AutoCAD 2021: the stem (19, 100)-(19, -50), whose last element stands at -1.
            CadDocument document = Four("0", Square, Ml(19, 100, 19, -50));

            CadCommandSession session = Edit(document, "MLEDIT", "OT", P(19, 50), P(150, 0));

            Assert.Contains(SelfIntersecting, session.History);
            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[1], 1, None, None, None, Whole);
            Tails(mlines[0], 0, Whole, Whole, Whole, Whole);
        }

        [Fact]
        public void AMergedTeeWhoseOuterElementsMeetTheClosedMlinePastItsCorner_RunsTheInnerOnesOn()
        {
            // AutoCAD 2021: the stem (5, 100)-(5, -50), whose elements stand 5, 0, -10 and -15: the second runs 5 on to the
            // bar's second element, the last two meet the bar before its start.
            CadDocument document = Four("0", Square, Ml(5, 100, 5, -50));

            CadCommandSession session = Edit(document, "MLEDIT", "MT", P(5, 50), P(150, 0));

            Assert.Contains(SelfIntersecting, session.History);
            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[1], 1, None, new[] { 0.0, 5.0 }, Whole, Whole);
            Tails(mlines[0], 0, Whole, Whole, Whole, Whole);
        }

        [Fact]
        public void ATeeWhoseElementsMeetTheClosedMlineAtItsStartExactly_IsMade()
        {
            // AutoCAD 2021: the stem (21, 100)-(21, -50), whose last element stands at 1, opens the bar from 1 to 21.
            CadDocument document = Four("0", Square, Ml(21, 100, 21, -50));

            CadCommandSession session = Edit(document, "MLEDIT", "OT", P(21, 50), P(150, 0), Enter);

            Assert.DoesNotContain(SelfIntersecting, session.History);
            Tails(Mlines(document)[0], 0, new[] { 0.0, 1.0, 21.0 }, Whole, Whole, Whole);
            Tails(Mlines(document)[1], 1, None, None, None, None);
        }

        [Fact]
        public void ATeeBeyondTheEndOfASegmentThatTurnsIn_StopsTheElementsThatMeetIt_AndRefusesTheRest()
        {
            // AutoCAD 2021: the bar (0, 0)-(200, 0)-(200, -200) turns right, so that its element 20 below the line ends at
            // x = 180; the stem (170, -100)-(170, 50) stands at 170, 175, 185 and 190: the last two pass it.
            CadDocument document = Four("0", Ml(0, 0, 200, 0, 200, -200), Ml(170, -100, 170, 50));

            CadCommandSession session = Edit(document, "MLEDIT", "OT", P(170, -50), P(100, 0));

            Assert.Contains(SelfIntersecting, session.History);
            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[1], 0, new[] { 0.0, 80.0 }, new[] { 0.0, 80.0 }, Whole, Whole);
            Tails(mlines[1], 1, None, None, Whole, Whole);
            Tails(mlines[0], 0, Whole, Whole, Whole, Whole);
        }

        [Fact]
        public void ATeeKeepingTheEndBeyondTheClosedMlinesInnerSide_StartsTheElementsThatMeetIt_AndRefusesTheRest()
        {
            // AutoCAD 2021: the stem (185, -50)-(185, 100) picked at (185, 50), kept from (185, 0): its elements stand at 185,
            // 190, 200 and 205, and the bar's near element ends at 200.
            CadDocument document = Four("0", Square, Ml(185, -50, 185, 100));

            CadCommandSession session = Edit(document, "MLEDIT", "MT", P(185, 50), P(150, 0));

            Assert.Contains(SelfIntersecting, session.History);
            List<DxfUnknownEntity> mlines = Mlines(document);
            Points(mlines[1], P(185, 0), P(185, 100));
            Tails(mlines[1], 0, Whole, new[] { -5.0 }, new[] { -5.0 }, Whole);
            Tails(mlines[0], 0, Whole, Whole, Whole, Whole);
        }

        [Theory]
        [InlineData("OT")]
        [InlineData("MT")]
        public void ATeeOnTheClosingSegmentOfAClosedMline_FromOutside_BreaksTheFarElement(string option)
        {
            CadDocument document = Drawing(Square, Ml(-100, 100, 50, 100));

            Edit(document, "MLEDIT", option, P(-50, 100), P(0, 150), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 3, Whole, new[] { 0.0, 120.0, 140.0 });
            Points(mlines[1], P(-100, 100), P(0, 100));
            Tails(mlines[1], 0, new[] { 0.0, 80.0 }, new[] { 0.0, 80.0 });
        }

        /// <summary>TRIM or EXTEND in Standard mode, every object an edge, picking at a point.</summary>
        private static CadCommandSession Cut(CadDocument document, string command, GeoPoint2 pick) =>
            Edit(document, command, "O", "S", Enter, pick, Enter);

        /// <summary>TRIM or EXTEND in Standard mode with Edge set to Extend, every object an edge, picking at a point.</summary>
        private static CadCommandSession CutExtending(CadDocument document, string command, GeoPoint2 pick) =>
            Edit(document, command, "O", "S", Enter, "Edge", "Extend", pick, Enter);

        [Fact]
        public void Extend_ToAnEdgeThatReachesOneElementOnlyUntilItIsExtended_RunsTheEndToTheFurthestElement()
        {
            // AutoCAD 2021, Edge = Extend: the edge (140, -10)-(150, -30) crosses the bottom element at x = 145 and, run on,
            // the top one at x = 135.
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(140, -10, 150, -30));

            CutExtending(document, "EXTEND", P(90, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(145, 0));
            Tails(mline, 0, new[] { 0.0, 135.0 }, Whole);
            Tails(mline, 1, None, Whole);
        }

        [Fact]
        public void Extend_ToAnObliqueEdgeRunOn_StopsTheElementThatMeetsItShort()
        {
            // AutoCAD 2021, Edge = Extend: the edge (140, 10)-(150, 30) run on meets the top element at x = 135 and the
            // bottom one at x = 125.
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(140, 10, 150, 30));

            CutExtending(document, "EXTEND", P(90, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(135, 0));
            Tails(mline, 0, Whole, new[] { 0.0, 125.0 });
            Tails(mline, 1, Whole, None);
        }

        [Fact]
        public void Extend_ToAnEdgeRunOn_ThatOneElementMeetsBehindTheEnd_LeavesTheMlineAsItIs()
        {
            // AutoCAD 2021: the edge (110, -30)-(90, 10) meets the top element at x = 95, behind the end.
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(110, -30, 90, 10));

            CutExtending(document, "EXTEND", P(90, 0));

            Points(Assert.Single(Mlines(document)), P(0, 0), P(100, 0));
        }

        [Fact]
        public void Extend_ToAnEdgeOneElementMisses_WithoutEdgeExtend_LeavesTheMlineAsItIs()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(140, -10, 150, -30));

            Cut(document, "EXTEND", P(90, 0));

            Points(Assert.Single(Mlines(document)), P(0, 0), P(100, 0));
        }

        [Fact]
        public void Extend_TheLastSegmentOfABentMline_ToAnObliqueEdge_StopsTheNearerElement()
        {
            // AutoCAD 2021: the edge (50, 140)-(150, 160) meets the line at y = 150 and the element 20 to its right at 154.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(50, 140, 150, 160));

            Cut(document, "EXTEND", P(100, 70));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(100, 0), P(100, 154));
            Tails(mline, 1, new[] { 0.0, 150.0 }, Whole);
            Tails(mline, 2, None, Whole);
        }

        [Fact]
        public void Trim_WhereTheElementsMeetTheEdgeOnDifferentSegments_CutsEachAtItsOwnHit_AtTheEnd()
        {
            // AutoCAD 2021: the edge (120, -30)-(95, 20) meets the top element on the second segment at (100, 10) and the
            // bottom one on the first at x = 115; picked past it, the multiline ends at (100, 10).
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(120, -30, 95, 20));

            Cut(document, "TRIM", P(100, 70));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(100, 0), P(100, 10));
            Tails(mline, 0, Whole, new[] { 0.0, 115.0 });
            Tails(mline, 1, new[] { 0.0, 10.0 }, None);
            Tails(mline, 2, None, None);
        }

        [Fact]
        public void Trim_WhereTheElementsMeetTheEdgeOnDifferentSegments_CutsEachAtItsOwnHit_AtTheStart()
        {
            // AutoCAD 2021: picked before it, the multiline starts at x = 115 on its first segment, whose top element is cut
            // away whole, and the top element starts 10 up the second.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(120, -30, 95, 20));

            Cut(document, "TRIM", P(50, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(115, 0), P(100, 0), P(100, 100));
            Tails(mline, 0, None, Whole);
            Tails(mline, 1, new[] { 10.0 }, Whole);
            Tails(mline, 2, Whole, Whole);
        }

        [Fact]
        public void Trim_AtABendThatTurnsRight_WhereTheInnerElementMeetsTheEdgeOnTheSecondSegment_StartsAtTheOuterHit()
        {
            // AutoCAD 2021: the multiline (0, 0)-(100, 0)-(100, -100), the edge (96, 10)-(66, -50) meets the top element at
            // x = 91 on the first segment and the bottom one on the second, 2 on from where it stands there.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, -100), L(96, 10, 66, -50));

            Cut(document, "TRIM", P(30, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(91, 0), P(100, 0), P(100, -100));
            Tails(mline, 0, Whole, None);
            Tails(mline, 1, Whole, new[] { 2.0 });
            Tails(mline, 2, Whole, Whole);
        }

        [Fact]
        public void Trim_AtABendThatTurnsRight_WhereTheInnerElementMeetsTheEdgeOnTheSecondSegment_EndsOnTheSecond()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, -100), L(96, 10, 66, -50));

            Cut(document, "TRIM", P(100, -70));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(100, 0), P(100, -22));
            Tails(mline, 0, new[] { 0.0, 91.0 }, Whole);
            Tails(mline, 1, None, new[] { 0.0, 2.0 });
            Tails(mline, 2, None, None);
        }

        [Theory]
        [InlineData(20.0, 160.0, new double[] { 0.0 })]
        [InlineData(20.0, 90.0, new double[] { 0.0, 120.0 })]
        [InlineData(80.0, 160.0, new double[] { 0.0, 60.0 })]
        [InlineData(70.0, 100.0, new double[] { 0.0, 60.0, 120.0 })]
        public void Weld_TakesOutEveryStopAndStartBetweenThePicks_ASingleOneLeavingTheCutTurnedInsideOut(
            double from,
            double to,
            double[] bottom
        )
        {
            // AutoCAD 2021: the bottom element of (0, 0)-(200, 0) cut alone from 60 to 120 ([0, 60, 120] on it), then welded
            // between two picks on the top one. Both its stop and its start in the range make it whole; one alone is taken
            // out and the other left, so that it is drawn only up to 120 or only up to 60.
            CadDocument document = Drawing(Ml(0, 0, 200, 0));
            Edit(document, "MLEDIT", "CS", P(60, -20), P(120, -20), Enter);

            Edit(document, "MLEDIT", "WA", P(from, 0), P(to, 0), Enter);

            Tails(Assert.Single(Mlines(document)), 0, Whole, bottom);
        }

        [Fact]
        public void Weld_ReachingAnotherSegment_JoinsTheCutThatLiesBetweenThePicks()
        {
            // AutoCAD 2021: CA from (50, 0) to (100, 50), then WA from (40, 0) to (100, 70): whole again.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            Edit(document, "MLEDIT", "CA", P(50, 0), P(100, 50), Enter);

            Edit(document, "MLEDIT", "WA", P(40, 0), P(100, 70), Enter);

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Tails(mline, 0, Whole, Whole);
            Tails(mline, 1, Whole, Whole);
        }

        [Fact]
        public void Weld_WithAPickOnTheCutItself_FindsNothingThere_AndLeavesTheCutAsItWas()
        {
            // AutoCAD 2021: CA from (50, 0) to (100, 50), then WA to (100, 30), where nothing of the multiline is drawn.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            Edit(document, "MLEDIT", "CA", P(50, 0), P(100, 50), Enter);

            Edit(document, "MLEDIT", "WA", P(40, 0), P(100, 30), Enter);

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Tails(mline, 0, new[] { 0.0, 50.0 }, new[] { 0.0, 50.0 });
            Tails(mline, 1, new[] { 50.0 }, new[] { 70.0 });
        }

        [Fact]
        public void Weld_RoundTheFirstVertexOfAClosedMline_LeavesTheOuterElementTurnedInsideOut_AsAutoCADDoes()
        {
            // AutoCAD 2021 welds the top element of the square cut from (0, 50) to (50, 0) back to whole, but leaves the
            // bottom one, which stands 20 further out at the corner, as [0, 20] on the first segment: the stop at the vertex
            // the cut started at stays, and the start at 70 goes, so it is drawn only before the vertex.
            CadDocument document = Drawing(Closed(0, 0, 100, 0, 100, 100, 0, 100));
            Edit(document, "MLEDIT", "CA", P(0, 50), P(50, 0), Enter);

            Edit(document, "MLEDIT", "WA", P(0, 50), P(50, 0), Enter);

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Tails(mline, 3, Whole, Whole);
            Tails(mline, 0, Whole, new[] { 0.0, 20.0 });
            Tails(mline, 1, Whole, Whole);
        }

        [Fact]
        public void Weld_RoundTheFirstVertexOfAClosedMline_WithPicksInTheCut_FindsNothingThere()
        {
            // AutoCAD 2021: the cut from (0, 50) to (50, 0), welded between (0, 70) and (20, 0), the second on the cut: unchanged.
            CadDocument document = Drawing(Closed(0, 0, 100, 0, 100, 100, 0, 100));
            Edit(document, "MLEDIT", "CA", P(0, 50), P(50, 0), Enter);

            Edit(document, "MLEDIT", "WA", P(0, 70), P(20, 0), Enter);

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Tails(mline, 3, new[] { 0.0, 50.0 }, new[] { 0.0, 70.0 });
            Tails(mline, 0, new[] { 50.0 }, new[] { 0.0, 20.0, 70.0 });
        }

        [Fact]
        public void AClosedMline_HasAGripAtEachVertex_AndTheOneAtTheFirstDragsThatVertex()
        {
            // AutoCAD 2021's GetGripPoints of the closed square: (0, 0), (100, 0), (100, 100), (0, 100); and dragging the
            // first moves the vertex, whole segments left as they are drawn.
            CadDocument document = Drawing(Closed(0, 0, 100, 0, 100, 100, 0, 100));
            DxfUnknownEntity mline = Mlines(document)[0];

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mline);
            Assert.Equal(4, grips.Count);
            Near(P(0, 100), grips[3].Point);

            Assert.True(document.GripDrag(mline.Handle, 0, P(-10, -10)).Succeeded);

            mline = Mlines(document)[0];
            Assert.True(new DxfMlineBuilder(mline).IsClosed);
            Points(mline, P(-10, -10), P(100, 0), P(100, 100), P(0, 100));
        }

        [Fact]
        public void Trim_WhereAnEdgeMeetsOneElementTwiceAndTheOtherOnce_LeavesTheMlineAsItIs()
        {
            // AutoCAD 2021: the edge (60, -5)-(130, 6.67) meets the top element on both segments, the bottom one on the second.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(60, -5, 130, 6.6666666666666667));

            Cut(document, "TRIM", P(100, 70));

            Points(Assert.Single(Mlines(document)), P(0, 0), P(100, 0), P(100, 100));
        }
    }
}
