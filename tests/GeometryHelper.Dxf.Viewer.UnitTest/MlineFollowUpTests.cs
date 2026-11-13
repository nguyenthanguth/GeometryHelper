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
    /// The follow-ups of MLINE: its grips, STRETCH, TRIM, EXTEND and the commands that refuse it, MLEDIT's empty picks, closed
    /// multilines, cuts across segments and the tees of four elements, against the records AutoCAD 2021 left (accoreconsole,
    /// <c>Wblock</c> of the multilines into a side database written by <c>DxfOut</c>). Unless said, the style STANDARD at
    /// scale 20 justified at the top, so that the second element stands 20 to the right of the line.
    /// </summary>
    public class MlineFollowUpTests
    {
        private const double Close = 1E-9;
        private const double Third = 46.666666666666664;

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

        /// <summary>A drawing whose current style FOUR has the elements 1, 0.5, -0.5 and -1, at scale 10 justified at zero, as the probe made it.</summary>
        private static CadDocument Four(params object[][] lines)
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
            Run(session, "CMLSTYLE", "FOUR", "CMLSCALE", "10", "CMLJUST", "1");

            foreach (object[] line in lines)
            {
                Run(session, "MLINE");
                Run(session, line);
                Run(session, Enter);
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

        /// <summary>TRIM or EXTEND in Standard mode, every object an edge, picking at a point.</summary>
        private static CadCommandSession Cut(CadDocument document, string command, GeoPoint2 pick) =>
            Edit(document, command, "O", "S", Enter, pick, Enter);

        [Fact]
        public void TheGripsOfAnMline_AreItsVertices_AndDraggingOneMovesTheVertex()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            DxfUnknownEntity mline = Mlines(document)[0];

            // AutoCAD 2021's GetGripPoints: (0, 0), (100, 0), (100, 100).
            IReadOnlyList<CadGrip> grips = CadGrips.Of(mline);
            Assert.Equal(3, grips.Count);
            Assert.All(grips, g => Assert.Equal(CadGripKind.Vertex, g.Kind));
            Near(P(100, 0), grips[1].Point);

            Assert.True(document.GripDrag(mline.Handle, 1, P(110, 5)).Succeeded);

            Points(Mlines(document)[0], P(0, 0), P(110, 5), P(100, 100));
        }

        [Fact]
        public void AGripDragged_KeepsTheCutOfTheSegmentThatKeepsItsWay_AndMakesTheTurnedOneWhole()
        {
            // AutoCAD 2021: cuts all 30 to 60 on both segments, then MoveVertexAt (1, (100, -10)).
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            Edit(document, "MLEDIT", "CA", P(30, 0), P(60, 0), Enter);
            Edit(document, "MLEDIT", "CA", P(100, 30), P(100, 60), Enter);
            DxfUnknownEntity mline = Mlines(document)[0];

            Assert.True(document.GripDrag(mline.Handle, 1, P(100, -10)).Succeeded);

            mline = Mlines(document)[0];
            Tails(mline, 0, Whole, Whole);
            Tails(mline, 1, new[] { 0.0, 40.0, 70.0 }, new[] { 0.0, 62.09975124224175, 92.09975124224175 });
        }

        [Fact]
        public void Stretch_MovesTheVerticesInsideItsWindow_KeepingACutWhileTheSegmentKeepsItsWay()
        {
            // AutoCAD 2021: cut all 30 to 60, then STRETCH the vertex (100, 0) by (20, 0), then (0, 0) by (0, 15).
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            Edit(document, "MLEDIT", "CA", P(30, 0), P(60, 0), Enter);

            Edit(document, "STRETCH", "C", P(90, -30), P(130, 10), Enter, P(0, 0), P(20, 0));

            DxfUnknownEntity mline = Mlines(document)[0];
            Points(mline, P(0, 0), P(120, 0), P(100, 100));
            Tails(mline, 0, new[] { 0.0, 30.0, 60.0 }, new[] { 0.0, 30.0, 60.0 });

            Edit(document, "STRETCH", "C", P(-10, -30), P(10, 10), Enter, P(0, 0), P(0, 15));

            mline = Mlines(document)[0];
            Points(mline, P(0, 15), P(120, 0), P(100, 100));
            Tails(mline, 0, Whole, Whole);
        }

        [Fact]
        public void Trim_ByAnObliqueEdge_PickedBeforeIt_StartsTheMlineWhereTheFirstElementMeetsTheEdge()
        {
            // AutoCAD 2021: the edge (40, -60) to (60, 60) meets the top element at 50 and the bottom one at 46.67.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(40, -60, 60, 60));

            Cut(document, "TRIM", P(20, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(Third, 0), P(100, 0), P(100, 100));
            Tails(mline, 0, new[] { 50.0 - Third }, Whole);
        }

        [Fact]
        public void Trim_ByAnObliqueEdge_PickedPastIt_EndsTheMlineWhereTheLastElementMeetsTheEdge()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(40, -60, 60, 60));

            Cut(document, "TRIM", P(80, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(50, 0));
            Tails(mline, 0, new[] { 0.0, 50.0 }, new[] { 0.0, Third });
            Tails(mline, 1, None, None);
        }

        [Fact]
        public void Trim_ByAnEdgeThatCrossesOneElementOnly_LeavesTheMlineAsItIs()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100), L(50, -10, 50, 60));

            Cut(document, "TRIM", P(30, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(100, 0), P(100, 100));
            Tails(mline, 0, Whole, Whole);
        }

        [Fact]
        public void Trim_OfThePieceBetweenTwoEdges_LeavesTwoMlines()
        {
            // AutoCAD 2021: edges at x = 50 and (120, -60) to (140, 60); the pick at 80 leaves (0, 0)-(50, 0) and (126.67, 0)-(200, 0).
            CadDocument document = Drawing(Ml(0, 0, 200, 0), L(50, -60, 50, 60), L(120, -60, 140, 60));

            Cut(document, "TRIM", P(80, 0));

            List<DxfUnknownEntity> mlines = Mlines(document);
            Assert.Equal(2, mlines.Count);
            Points(mlines[0], P(0, 0), P(50, 0));
            Tails(mlines[0], 0, new[] { 0.0, 50.0 }, new[] { 0.0, 50.0 });
            Points(mlines[1], P(80 + Third, 0), P(200, 0));
            Tails(mlines[1], 0, new[] { 130.0 - 80 - Third }, Whole);
        }

        [Fact]
        public void Trim_OfAClosedMline_OpensItBetweenTheTwoCutsRoundThePick()
        {
            // AutoCAD 2021: the square cut by x = 50 and picked at (30, 0) is left (50, 0), (100, 0), (100, 100), (50, 100).
            CadDocument document = Drawing(Closed(0, 0, 100, 0, 100, 100, 0, 100), L(50, -60, 50, 160));

            Cut(document, "TRIM", P(30, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Assert.False(new DxfMlineBuilder(mline).IsClosed);
            Points(mline, P(50, 0), P(100, 0), P(100, 100), P(50, 100));
            Tails(mline, 0, Whole, Whole);
            Tails(mline, 2, new[] { 0.0, 50.0 }, new[] { 0.0, 70.0 });
            Tails(mline, 3, None, None);
        }

        [Fact]
        public void Trim_OfTheStart_KeepsACutFurtherOnWhereItWas()
        {
            // AutoCAD 2021: cut all 120 to 150, then trimmed at x = 50: the cut 70 to 100 from the new start.
            CadDocument document = Drawing(Ml(0, 0, 200, 0));
            Edit(document, "MLEDIT", "CA", P(120, 0), P(150, 0), Enter);
            Edit(document, "LINE", P(50, -60), P(50, 60), Enter);

            Cut(document, "TRIM", P(20, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(50, 0), P(200, 0));
            Tails(mline, 0, new[] { 0.0, 70.0, 100.0 }, new[] { 0.0, 70.0, 100.0 });
        }

        [Fact]
        public void Extend_ToAnObliqueEdge_RunsTheEndToWhereTheFurthestElementMeetsIt_AndStopsTheOther()
        {
            // AutoCAD 2021: to (140, -60)-(160, 60), the end goes to (150, 0) and the bottom element stops at 146.67.
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(140, -60, 160, 60));

            Cut(document, "EXTEND", P(90, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(0, 0), P(150, 0));
            Tails(mline, 0, Whole, new[] { 0.0, 100 + Third });
        }

        [Fact]
        public void Extend_ToAnEdgeOneElementMisses_LeavesIt_AndToOneBothMeet_RunsOn()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(150, -30, 150, -10));

            Cut(document, "EXTEND", P(90, -20));
            Points(Mlines(document)[0], P(0, 0), P(100, 0));

            Edit(document, "LINE", P(150, -30), P(150, 10), Enter);
            Cut(document, "EXTEND", P(90, -20));
            Points(Mlines(document)[0], P(0, 0), P(150, 0));
        }

        [Fact]
        public void Extend_OfTheStart_RunsTheFirstVertexBack()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0), L(-50, -60, -50, 60));

            Cut(document, "EXTEND", P(10, 0));

            DxfUnknownEntity mline = Assert.Single(Mlines(document));
            Points(mline, P(-50, 0), P(100, 0));
            Tails(mline, 0, Whole, Whole);
        }

        [Fact]
        public void AnMline_IsACuttingEdgeOfALine_AtItsElements()
        {
            // AutoCAD 2021: the line (50, -60)-(50, 60) picked at (50, 50) is trimmed at the top element, y = 0.
            CadDocument document = Drawing(Ml(0, 0, 200, 0), L(50, -60, 50, 60));

            Cut(document, "TRIM", P(50, 50));

            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(50, -60, 0), line.StartPoint);
            Near(new GeoPoint3(50, 0, 0), line.EndPoint);
        }

        [Fact]
        public void Offset_Fillet_Chamfer_Lengthen_AndBreak_RefuseAnMline_InAutoCADsWords()
        {
            CadDocument document = Drawing(Ml(0, 200, 100, 200), Ml(200, 100, 200, 180));

            Assert.Contains(
                "Cannot offset that object.",
                Edit(document, "OFFSET", "10", P(50, 200), P(50, 250), Enter).History
            );
            CadCommandSession fillet = Edit(document, "FILLET", P(50, 200));
            Assert.Contains("Fillet requires 2 lines, arcs, or circles.", fillet.History);
            Assert.StartsWith("Select first object", fillet.PromptText);
            Assert.Contains("Chamfer requires 2 lines.", Edit(document, "CHAMFER", P(50, 200)).History);
            Assert.Contains("Cannot LENGTHEN this object.", Edit(document, "LENGTHEN", "DE", "10", P(50, 200)).History);
            CadCommandSession broken = Edit(document, "BREAK", P(50, 200));
            Assert.Contains("Object can't be broken", broken.History);
            Assert.Equal("Select object:", broken.PromptText);
            Assert.Equal(2, Mlines(document).Count);
        }

        [Fact]
        public void Mledit_APickOnNothing_AsksAgainWithoutAWord_AndEnterBeforeAnEditIsRefused()
        {
            CadDocument document = Drawing(Ml(0, 0, 100, 0), Ml(50, -50, 50, 50));
            CadCommandSession session = Edit(document, "MLEDIT", "CC", P(-200, -200));

            Assert.DoesNotContain("Nothing selected.", session.History);
            Assert.Equal("Select first mline:", session.PromptText);

            Run(session, Enter);
            Assert.Contains("2D point or option keyword required.", session.History);
            Assert.Equal("Select first mline:", session.PromptText);
        }

        [Fact]
        public void Mledit_ACornerJointOfAClosedMline_OrATeeWhoseStemIsClosed_IsRefused()
        {
            CadDocument document = Drawing(Closed(0, 0, 200, 0, 200, 200, 0, 200), Ml(100, 100, 100, -50));

            Assert.Contains(
                "Invalid operation on a closed multiline.",
                Edit(document, "MLEDIT", "CJ", P(100, 50), P(150, 0)).History
            );
            Assert.Contains(
                "Invalid operation on a closed multiline.",
                Edit(document, "MLEDIT", "CT", P(150, 0), P(100, 50)).History
            );
            Points(Mlines(document)[1], P(100, 100), P(100, -50));
        }

        [Fact]
        public void Mledit_AClosedTeeOnAClosedMline_TrimsTheStemToItsNearElement_AndLeavesItWhole()
        {
            // AutoCAD 2021: the stem ends at (100, 0), its end caps left off; the square keeps every element whole.
            CadDocument document = Drawing(Closed(0, 0, 200, 0, 200, 200, 0, 200), Ml(100, 100, 100, -50));

            Edit(document, "MLEDIT", "CT", P(100, 50), P(150, 0), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Points(mlines[1], P(100, 100), P(100, 0));
            Tails(mlines[1], 0, Whole, Whole);
            Tails(mlines[1], 1, None, None);
            Assert.True(new DxfMlineBuilder(mlines[1]).SuppressEndCaps);

            for (int v = 0; v < 4; v++)
            {
                Tails(mlines[0], v, Whole, Whole);
            }
        }

        [Fact]
        public void Mledit_CutAll_FromOneSegmentToTheNext_CutsTheCornerBetween()
        {
            // AutoCAD 2021: CA from (50, 0) to (100, 50).
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));

            Edit(document, "MLEDIT", "CA", P(50, 0), P(100, 50), Enter);

            DxfUnknownEntity mline = Mlines(document)[0];
            Tails(mline, 0, new[] { 0.0, 50.0 }, new[] { 0.0, 50.0 });
            Tails(mline, 1, new[] { 50.0 }, new[] { 70.0 });
        }

        [Fact]
        public void Mledit_CutSingle_FromOneSegmentToTheNext_CutsThePickedElementOnly()
        {
            // AutoCAD 2021: CA from (50, 0) to (100, 50), then CS from (20, 0) to (100, 80) on the top element.
            CadDocument document = Drawing(Ml(0, 0, 100, 0, 100, 100));
            Edit(document, "MLEDIT", "CA", P(50, 0), P(100, 50), Enter);

            Edit(document, "MLEDIT", "CS", P(20, 0), P(100, 80), Enter);

            DxfUnknownEntity mline = Mlines(document)[0];
            Tails(mline, 0, new[] { 0.0, 20.0 }, new[] { 0.0, 50.0 });
            Tails(mline, 1, new[] { 80.0 }, new[] { 70.0 });
        }

        [Fact]
        public void Mledit_CutAll_RoundTheFirstVertexOfAClosedMline_StartsOnTheFirstSegmentAtTheVertex()
        {
            // AutoCAD 2021: the square cut from (0, 50) on its last segment to (50, 0) on its first.
            CadDocument document = Drawing(Closed(0, 0, 100, 0, 100, 100, 0, 100));

            Edit(document, "MLEDIT", "CA", P(0, 50), P(50, 0), Enter);

            DxfUnknownEntity mline = Mlines(document)[0];
            Tails(mline, 3, new[] { 0.0, 50.0 }, new[] { 0.0, 70.0 });
            Tails(mline, 0, new[] { 50.0 }, new[] { 0.0, 20.0, 70.0 });
        }

        [Fact]
        public void Mledit_MergedTeeOfFourElements_JoinsThemPairByPairFromTheOutside()
        {
            // AutoCAD 2021, FOUR at scale 10 justified at zero: the bar (0, 0)-(200, 0), the stem (100, 100)-(100, 0).
            CadDocument document = Four(Ml(0, 0, 200, 0), Ml(100, 100, 100, 0));

            Edit(document, "MLEDIT", "MT", P(105, 50), P(150, 5), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, new[] { 0.0, 90.0, 110.0 }, new[] { 0.0, 95.0, 105.0 }, Whole, Whole);
            Tails(mlines[1], 0, new[] { 0.0, 90.0 }, new[] { 0.0, 95.0 }, new[] { 0.0, 95.0 }, new[] { 0.0, 90.0 });
            Tails(mlines[1], 1, None, None, None, None);
        }

        [Fact]
        public void Mledit_MergedCrossOfFourElements_BreaksEachPairAcrossTheOtherPair()
        {
            CadDocument document = Four(Ml(0, 0, 200, 0), Ml(100, 100, 100, -100));

            Edit(document, "MLEDIT", "MC", P(105, 50), P(150, 5), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            double[] outer = { 0.0, 90.0, 110.0 };
            double[] inner = { 0.0, 95.0, 105.0 };
            Tails(mlines[0], 0, outer, inner, inner, outer);
            Tails(mlines[1], 0, outer, inner, inner, outer);
        }

        [Theory]
        [InlineData("CT", false)]
        [InlineData("OT", true)]
        public void Mledit_ClosedAndOpenTeesOfFourElements_StopTheStemAtTheBarsNearElement(string option, bool opens)
        {
            CadDocument document = Four(Ml(0, 0, 200, 0), Ml(100, 100, 100, 0));

            Edit(document, "MLEDIT", option, P(105, 50), P(150, 5), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            Tails(mlines[0], 0, opens ? new[] { 0.0, 90.0, 110.0 } : Whole, Whole, Whole, Whole);
            double[] stop = { 0.0, 90.0 };
            Tails(mlines[1], 0, stop, stop, stop, stop);
        }

        [Fact]
        public void MlstyleRename_RenamesAStyleAndItsMultilines_ButNotStandard()
        {
            CadDocument document = Four(Ml(0, 0, 100, 0));

            CadCommandSession session = Edit(document, "MLSTYLE", "Rename", Enter, "Walls");

            Assert.True(document.Database.MLStyleDictionary.Contains("Walls"));
            Assert.False(document.Database.MLStyleDictionary.Contains("FOUR"));
            Assert.Contains(Mlines(document)[0].Groups, g => g.TypeCode == 2 && g.Value.Trim() == "WALLS");
            Assert.Contains("Current multiline style: WALLS", session.History);

            Run(session, "Rename", "STANDARD");
            Assert.Contains("The STANDARD multiline style cannot be renamed.", session.History);
        }

        [Fact]
        public void MlstyleDelete_RemovesAStyleNothingUses_AndKeepsStandardTheCurrentAndOneInUse()
        {
            CadDocument document = Four(Ml(0, 0, 100, 0));
            var spare = new DxfMlineStyleSettings();
            document.Database.AddMlineStyle("SPARE", spare);

            CadCommandSession session = Edit(document, "MLSTYLE", "Delete", "spare");
            Assert.False(document.Database.MLStyleDictionary.Contains("SPARE"));

            Run(session, "Delete", "FOUR");
            Assert.Contains(session.History, h =>
                h.StartsWith("The multiline style FOUR cannot be deleted", StringComparison.Ordinal)
            );
            Run(session, "Delete", "STANDARD");
            Assert.True(document.Database.MLStyleDictionary.Contains("FOUR"));
            Assert.True(document.Database.MLStyleDictionary.Contains("STANDARD"));
        }

        [Fact]
        public void MlstyleSaveThenLoad_CarriesAStyleToAnotherDrawingThroughAnMlnFile()
        {
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-mln-" + Guid.NewGuid().ToString("N")
            );
            System.IO.Directory.CreateDirectory(folder);

            try
            {
                string file = System.IO.Path.Combine(folder, "walls.mln");
                CadDocument from = Four(Ml(0, 0, 100, 0));
                CadCommandSession saving = Edit(from, "MLSTYLE", "Save", "FOUR", file);
                Assert.Contains("Multiline style FOUR saved to walls.mln.", saving.History);

                CadDocument to = Blank();
                CadCommandSession loading = Edit(to, "MLSTYLE", "Load", file, "?");
                Assert.Contains("  FOUR", loading.History);

                Run(loading, "four");
                var loaded = (DxfMlineStyle)to.Database.MLStyleDictionary.GetAt("FOUR");
                Assert.Equal(new[] { 1.0, 0.5, -0.5, -1.0 }, loaded.Elements.Select(e => e.Offset));

                Run(loading, "Load", file, "FOUR");
                Assert.Contains("Multiline style FOUR already exists.", loading.History);
                Run(loading, "Load", System.IO.Path.Combine(folder, "none.mln"));
                Assert.Contains(loading.History, h =>
                    h.StartsWith("Cannot find multiline style file", StringComparison.Ordinal)
                );
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }
    }
}
