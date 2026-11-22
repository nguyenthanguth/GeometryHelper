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
    /// The drawing commands the L2-parked follow-up (part C1) brought to AutoCAD 2021: what -HATCH stores for the objects it is
    /// given (edges, loop flags, the island style, the seed points), the Parameter option of ELLIPSE's arc and the groups it
    /// leaves past 2 pi, the names of DIMBLK and its relatives, and the Annotative option and the font names of -STYLE. Each
    /// row is marked with the console run it comes from: <c>l2p pc</c> and <c>l2p pb</c> (the probes of the spec), <c>l2p
    /// c1a</c>, <c>c1b</c> and <c>c1c</c> (this part's, in the C1 scratch) or <c>l2fix p3</c> and <c>p6</c>.
    /// </summary>
    public sealed class L2ParkedDrawTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Typed(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        // What a form gives, as the lines after it.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        private static string HandleOf(DxfEntity entity) => entity.Handle;

        /// <summary>A closed polyline of the corners given, each with the bulge after it.</summary>
        private static void Pline(DxfDatabase database, params double[] xyBulge)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i + 2 < xyBulge.Length + 1; i += 3)
            {
                polyline.AddVertexAt(i / 3, new GeoPoint2(xyBulge[i], xyBulge[i + 1]), xyBulge[i + 2], 0.0, 0.0);
            }

            polyline.Closed = true;
            database.ModelSpace.AppendEntity(polyline);
        }

        private static CadDocument Drawing(Action<DxfDatabase> fill)
        {
            var database = new DxfDatabase("AC1027");
            fill(database);
            return CadDocument.From(database, "draw.dxf");
        }

        private static CadDocument Rectangles(params double[] squares) =>
            Drawing(database =>
            {
                for (int s = 0; s + 2 < squares.Length + 1; s += 3)
                {
                    double x = squares[s];
                    double y = squares[s + 1];
                    double side = squares[s + 2];
                    Pline(database, x, y, 0, x + side, y, 0, x + side, y + side, 0, x, y + side, 0);
                }
            });

        private static string[] Handles(CadDocument document) =>
            All<DxfPolyline>(document.ModelSpace).Select(HandleOf).ToArray();

        // -HATCH of the objects given: Properties (ANSI31, 1, 0), Select objects, Enter, Enter.
        private static CadCommandSession HatchOf(CadDocument document, params string[] handles) =>
            Typed(document, "-HATCH", "P", "ANSI31", "1", "0", "S", handles, Enter, Enter);

        // The groups of the last entity between the first 93 and the first 97: the edges of the first loop.
        private const string Edges =
            "(defun gh-e (/ g r on) (setq g (entget (entlast))) (foreach x g (cond ((= (car x) 93) (setq on T)) ((= (car x) 97) (setq on nil)) (on (setq r (cons x r))))) (reverse r))";

        private const string Loops = "(mapcar 'cdr (vl-remove-if-not '(lambda (x) (= (car x) 92)) (entget (entlast))))";

        private const string Seeds = "(member (assoc 98 (entget (entlast))) (entget (entlast)))";

        #region -HATCH of the objects selected (probes pc and c1b)

        [Fact]
        public void HatchOfASelectedRectangle_StoresItsSidesAsLineEdges_FromTheFirstVertex()
        {
            // AutoCAD 2021 (l2p pc): RECTANG 0,0 10,10 then -HATCH S L gives 92 1, 93 4 and the edges 72 1 (0 0)-(10 0), (10 0)-(10
            // 10), (10 10)-(0 10), (0 10)-(0 0), no polyline loop; the presenter stored one loop of a polyline (92 3).
            CadDocument document = Rectangles(0, 0, 10);
            HatchOf(document, Handles(document));

            DxfHatchLoop loop = OnlyHatch(document).GetLoopAt(0);
            Assert.False(loop.IsPolyline);
            Assert.Equal(DxfHatchLoopTypes.External, loop.LoopType);
            Assert.Equal(4, loop.Curves.Count);
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0),
                new GeoPoint3(0, 10, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                GeoLine3 side = Assert.IsType<GeoLine3>(loop.Curves[i]);
                Near(corners[i], side.StartPoint, 1E-9);
                Near(corners[(i + 1) % 4], side.EndPoint, 1E-9);
            }
        }

        [Fact]
        public void HatchOfASelectedClockwisePolyline_KeepsItsDirection()
        {
            // AutoCAD 2021 (l2p c1b): PLINE 50,0 50,10 60,10 60,0 C then -HATCH S L gives the edges (50 0)-(50 10), (50 10)-(60 10),
            // (60 10)-(60 0), (60 0)-(50 0): the polyline's own order and way round.
            CadDocument document = Drawing(d => Pline(d, 50, 0, 0, 50, 10, 0, 60, 10, 0, 60, 0, 0));
            HatchOf(document, Handles(document));

            DxfHatchLoop loop = OnlyHatch(document).GetLoopAt(0);
            Assert.False(loop.IsPolyline);
            Near(new GeoPoint3(50, 0, 0), Assert.IsType<GeoLine3>(loop.Curves[0]).StartPoint, 1E-9);
            Near(new GeoPoint3(50, 10, 0), Assert.IsType<GeoLine3>(loop.Curves[0]).EndPoint, 1E-9);
            Near(new GeoPoint3(50, 0, 0), Assert.IsType<GeoLine3>(loop.Curves[3]).EndPoint, 1E-9);
        }

        [Fact]
        public void HatchOfASelectedPolylineWithABulge_StoresTheBulgedSideAsAnArcEdge()
        {
            // AutoCAD 2021 (l2p pc, c1b): PLINE 50,0 60,0 A 60,10 L 50,10 C gives line, arc, line, line, the arc 72 2 with centre
            // (60 5), radius 5, 50 4.71239, 51 7.85398, 73 1; drawn clockwise (80,10 to 90,10 over the top) it is 72 2 centre (85
            // 10), 50 3.14159, 51 6.28319, 73 0.
            CadDocument document = Drawing(d => Pline(d, 50, 0, 0, 60, 0, 1, 60, 10, 0, 50, 10, 0));
            HatchOf(document, Handles(document));

            DxfHatchLoop loop = OnlyHatch(document).GetLoopAt(0);
            Assert.Equal(4, loop.Curves.Count);
            Assert.IsType<GeoLine3>(loop.Curves[0]);
            GeoArc3 arc = Assert.IsType<GeoArc3>(loop.Curves[1]);
            Assert.Equal(5.0, arc.Radius, 6);
            Near(new GeoPoint3(60, 5, 0), arc.Center, 1E-6);
            Assert.False(arc.IsClockwise);
            Assert.IsType<GeoLine3>(loop.Curves[2]);
            Assert.IsType<GeoLine3>(loop.Curves[3]);

            CadDocument other = Drawing(d => Pline(d, 80, 0, 0, 80, 10, -1, 90, 10, 0, 90, 0, 0));
            CadCommandSession turned = HatchOf(other, Handles(other));
            GeoArc3 over = Assert.IsType<GeoArc3>(OnlyHatch(other).GetLoopAt(0).Curves[1]);
            Near(new GeoPoint3(85, 10, 0), over.Center, 1E-6);
            Assert.Equal("0", Value(turned, "(cdr (assoc 73 (entget (entlast))))"));
        }

        [Fact]
        public void HatchOfASelectedCircle_IsOneArcEdgeFromNoughtToAWholeTurn()
        {
            // AutoCAD 2021 (l2p pc): CIRCLE 30,0 5 then -HATCH S L gives 93 1, 72 2, 10 (30 0 0), 40 5, 50 0, 51 6.28319, 73 1;
            // the presenter stored a polyline of two vertices with the bulge 1 (92 3, 93 2).
            CadDocument document = Drawing(d =>
                d.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(30, 0, 0), new GeoVector3(0, 0, 1), 5.0))
            );
            string circle = All<DxfCircle>(document.ModelSpace).Single().Handle;
            Typed(document, "-HATCH", "P", "ANSI31", "1", "0", "S", new[] { circle }, Enter, Enter);

            DxfHatchLoop loop = OnlyHatch(document).GetLoopAt(0);
            Assert.False(loop.IsPolyline);
            Assert.Single(loop.Curves);
            GeoCircle3 whole = Assert.IsType<GeoCircle3>(loop.Curves[0]);
            Assert.Equal(5.0, whole.Radius, 9);
            Near(new GeoPoint3(30, 0, 0), whole.Center, 1E-9);
        }

        [Fact]
        public void ALoopInsideAnother_IsFlaggedOutermost_AndOneInsideThat_HasNoFlag()
        {
            // AutoCAD 2021 (l2p pc, c1c): RECTANG 0,20 20,40 and 5,25 15,35 selected give the loops 92 1 then 92 16; three
            // rectangles one in another, 92 0, 16, 1 (AutoCAD lists the inner first, the presenter the outer first).
            CadDocument document = Rectangles(0, 20, 20, 5, 25, 10);
            HatchOf(document, Handles(document));
            Assert.Equal(DxfHatchLoopTypes.External, OnlyHatch(document).GetLoopAt(0).LoopType);
            Assert.Equal(DxfHatchLoopTypes.Outermost, OnlyHatch(document).GetLoopAt(1).LoopType);

            CadDocument nested = Rectangles(0, 0, 30, 5, 5, 20, 10, 10, 10);
            nested.HatchSettings.IslandStyle = DxfHatchStyle.Normal;
            HatchOf(nested, Handles(nested));
            CadCommandSession session = Typed(nested);
            Assert.Equal("(1 16 0)", Value(session, Loops));
        }

        [Fact]
        public void TheEdgesOfAHatch_AreGivenToLispAsThreeDimensionalPoints()
        {
            // AutoCAD 2021 (l2p pc): entget of the hatch of RECTANG 0,0 10,10 holds (72 . 1) (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) ...;
            // the points of a hatch's edges and seeds are three-dimensional in the list though the file holds two coordinates.
            CadDocument document = Rectangles(0, 0, 10);
            CadCommandSession session = HatchOf(document, Handles(document));
            session.Feed(Edges);

            Assert.StartsWith(
                "((72 . 1) (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) (72 . 1) (10 10.0 0.0 0.0) (11 10.0 10.0 0.0)",
                Value(session, "(gh-e)"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AnEntmodOfAHatch_KeepsItsLoopsAsEdges()
        {
            // ours: the three-dimensional points of entget go back to the two coordinates of the file, so that the hatch the
            // list was read from is the hatch entmod writes.
            CadDocument document = Rectangles(0, 0, 10);
            CadCommandSession session = HatchOf(document, Handles(document));

            Assert.NotEqual("nil", Value(session, "(entmod (entget (entlast)))"));
            DxfHatchLoop loop = OnlyHatch(document).GetLoopAt(0);
            Assert.False(loop.IsPolyline);
            Assert.Equal(4, loop.Curves.Count);
            Near(new GeoPoint3(10, 10, 0), Assert.IsType<GeoLine3>(loop.Curves[1]).EndPoint, 1E-9);
        }

        [Fact]
        public void TheSeedOfAHatchOfSelectedObjects_IsTheOriginOnePoint()
        {
            // AutoCAD 2021 (l2p pc, c1c): every hatch of selected objects ends (98 . 1) (10 0.0 0.0 0.0), whatever the loops are.
            CadDocument document = Rectangles(0, 0, 10);
            CadCommandSession session = HatchOf(document, Handles(document));

            Assert.Equal("((98 . 1) (10 0.0 0.0 0.0))", Value(session, Seeds));
        }

        #endregion

        #region -HATCH by a point (probes pc and c1c)

        [Fact]
        public void TheSeedOfAHatchByAPoint_IsThePointPicked_OnceForEachLoop()
        {
            // AutoCAD 2021 (l2p pc, c1c): a point 2,2 in a rectangle gives (98 . 1) (10 2.0 2.0 0.0); a point 3,3 in three
            // rectangles one in another (91 3) gives (98 . 3) and the point three times.
            CadDocument one = Rectangles(0, 0, 10);
            CadCommandSession session = Typed(one, "-HATCH", "P", "ANSI31", "1", "0", P(2, 2), Enter);
            Assert.Equal("((98 . 1) (10 2.0 2.0 0.0))", Value(session, Seeds));

            CadDocument three = Rectangles(0, 0, 30, 5, 5, 20, 10, 10, 10);
            three.HatchSettings.IslandStyle = DxfHatchStyle.Normal;
            session = Typed(three, "-HATCH", "P", "ANSI31", "1", "0", P(3, 3), Enter);
            Assert.Equal("((98 . 3) (10 3.0 3.0 0.0) (10 3.0 3.0 0.0) (10 3.0 3.0 0.0))", Value(session, Seeds));
        }

        [Fact]
        public void AHatchByAPoint_FlagsTheLoopInsideTheOuterOneOutermost()
        {
            // AutoCAD 2021 (l2p c1c): the loops of a point in three rectangles one in another are 92 7, 22, 6: external, then
            // outermost, then none, each of a polyline and derived. The presenter flags the same but leaves out the derived
            // bit (4): a loop flagged derived makes AutoCAD 2021 expect the pixel size, group 47, which the library does not
            // write, and refuse the file.
            CadDocument document = Rectangles(0, 0, 30, 5, 5, 20, 10, 10, 10);
            document.HatchSettings.IslandStyle = DxfHatchStyle.Normal;
            Typed(document, "-HATCH", "P", "ANSI31", "1", "0", P(3, 3), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(3, hatch.NumberOfLoops);
            Assert.Equal(DxfHatchLoopTypes.External | DxfHatchLoopTypes.Polyline, hatch.GetLoopAt(0).LoopType);
            Assert.Equal(DxfHatchLoopTypes.Outermost | DxfHatchLoopTypes.Polyline, hatch.GetLoopAt(1).LoopType);
            Assert.Equal(DxfHatchLoopTypes.Polyline, hatch.GetLoopAt(2).LoopType);
        }

        #endregion

        #region HPISLANDDETECTION and the island style (probes pc and c1a)

        [Fact]
        public void HpIslandDetection_StartsAtOne_TakesNoughtToTwo_AndRefusesTheRest()
        {
            // AutoCAD 2021 (l2p pc, c1a): (getvar "HPISLANDDETECTION") is 1 on the blank drawing; 0 and 2 are set; 3 gives
            // "; error: AutoCAD variable setting rejected: "HPISLANDDETECTION" 3" and the value stays 2. The presenter gave nil.
            CadCommandSession session = Typed(Blank());

            Assert.Equal("1", Value(session, "(getvar \"HPISLANDDETECTION\")"));
            Assert.Equal("0", Value(session, "(setvar \"HPISLANDDETECTION\" 0)"));
            Assert.Equal("2", Value(session, "(setvar \"HPISLANDDETECTION\" 2)"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"HPISLANDDETECTION\" 3",
                Value(session, "(setvar \"HPISLANDDETECTION\" 3)")
            );
            Assert.Equal("2", Value(session, "(getvar \"HPISLANDDETECTION\")"));
        }

        [Fact]
        public void TheIslandStyleOfAHatch_IsWhatHpIslandDetectionHolds_AsGroup75()
        {
            // AutoCAD 2021 (l2p pc, c1c): the hatch of RECTANG 0,0 10,10 has (75 . 1) with HPISLANDDETECTION 1, (75 . 0) after the
            // variable was set to 0 and (75 . 2) after 2; the style prompt of -HATCH Advanced is "Enter hatching style
            // [Ignore/Outer/Normal] <Outer>:" at 1. The presenter wrote 75 0 and offered <Normal>.
            CadDocument document = Rectangles(0, 0, 10);
            CadCommandSession session = HatchOf(document, Handles(document));
            Assert.Equal("1", Value(session, "(cdr (assoc 75 (entget (entlast))))"));

            Value(session, "(setvar \"HPISLANDDETECTION\" 0)");
            Run(session, "-HATCH", "P", "ANSI31", "1", "0", "S", Handles(document), Enter, Enter);
            Assert.Equal("0", Value(session, "(cdr (assoc 75 (entget (entlast))))"));

            Value(session, "(setvar \"HPISLANDDETECTION\" 2)");
            Run(session, "-HATCH", "A", "S");
            Assert.Equal("Enter hatching style [Ignore/Outer/Normal] <Ignore>:", session.PromptText);
        }

        [Fact]
        public void TheStyleOfTheNextHatch_AndHpIslandDetection_AreOne()
        {
            // AutoCAD 2021 (l2p c1a): "Enter hatching style [Ignore/Outer/Normal] <Outer>:" on a blank drawing, where the
            // variable is 1; the style typed there is the variable's value from then on.
            CadDocument document = Blank();
            CadCommandSession session = Typed(document, "-HATCH", "A", "S");
            Assert.Equal("Enter hatching style [Ignore/Outer/Normal] <Outer>:", session.PromptText);

            Run(session, "N", Enter, Enter);
            Assert.Equal("0", Value(session, "(getvar \"HPISLANDDETECTION\")"));
        }

        [Fact]
        public void ThePatternPrompt_OffersSolidWithItsOwnCapital_AfterSolidWasUsed()
        {
            // AutoCAD 2021 (l2p pc): after -HATCH P SOLID the next -HATCH P asks "Enter a pattern name or [?/Solid/User
            // defined/Gradient] <Solid>:"; the presenter wrote <SOLID>.
            CadDocument document = Rectangles(0, 0, 10);
            CadCommandSession session = Typed(document, "-HATCH", "P", "SOLID", "S", Handles(document), Enter, Enter);
            Run(session, "-HATCH", "P");

            Assert.Equal("Enter a pattern name or [?/Solid/User defined/Gradient] <Solid>:", session.PromptText);
        }

        #endregion

        #region ELLIPSE Parameter (probes pc, p1 and p7)

        private static DxfEllipse Last(CadDocument document) => All<DxfEllipse>(document.ModelSpace).Last();

        [Fact]
        public void EllipseArc_AsksForTheStartAngleOrParameter_AndTheEndAngleOrParameter()
        {
            // AutoCAD 2021 (l2p pc): "Specify start angle or [Parameter]:", P gives "Specify start parameter or [Angle]:", and the
            // end prompts "Specify end angle or [Parameter/Included angle]:" and "Specify end parameter or [Angle/Included
            // angle]:"; A goes back to the angle prompt.
            CadCommandSession session = Typed(Blank(), "ELLIPSE", "A", P(50, 0), P(60, 0), "3");
            Assert.Equal("Specify start angle or [Parameter]:", session.PromptText);

            Run(session, "P");
            Assert.Equal("Specify start parameter or [Angle]:", session.PromptText);

            Run(session, "30");
            Assert.Equal("Specify end parameter or [Angle/Included angle]:", session.PromptText);

            Run(session, "A");
            Assert.Equal("Specify end angle or [Parameter/Included angle]:", session.PromptText);

            Run(session, "P");
            Assert.Equal("Specify end parameter or [Angle/Included angle]:", session.PromptText);
        }

        [Fact]
        public void EllipseArc_ByParameters_TakesThemInDegreesAsTheEccentricAngle()
        {
            // AutoCAD 2021 (l2p pc): ELLIPSE A 50,0 60,0 3 P 0 90 gives 41 0 and 42 1.5708 (start (50 0), end (55 -3)); a start
            // angle 30 then P 180, 0.766163 and 3.14159; P 30 A 60, 0.523599 and 1.23732; P 30 I 90, 42 2.0944; P 300 100,
            // 5.23599 and 8.02851 (the end past the start); P 0 400, 0 and 0.698132; ELLIPSE A 50,0 52,0 20 P 0 90, 0 and 1.5708.
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "P", "0", "90");
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(1.5708, Last(document).EndParam, 4);
            Near(new GeoPoint3(50, 0, 0), Last(document).StartPoint, 1E-6);
            Near(new GeoPoint3(55, -3, 0), Last(document).EndPoint, 1E-6);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "30", "P", "180");
            Assert.Equal(0.766163, Last(document).StartParam, 4);
            Assert.Equal(3.14159, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "P", "30", "A", "60");
            Assert.Equal(0.523599, Last(document).StartParam, 4);
            Assert.Equal(1.23732, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "P", "30", "I", "90");
            Assert.Equal(0.523599, Last(document).StartParam, 4);
            Assert.Equal(2.0944, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "P", "300", "100");
            Assert.Equal(5.23599, Last(document).StartParam, 4);
            Assert.Equal(8.02851, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "P", "0", "400");
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(0.698132, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(52, 0), "20", "P", "0", "90");
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(1.5708, Last(document).EndParam, 4);
        }

        [Fact]
        public void EllipseArc_ByAngles_KeepsTheEndPastTheStart_EvenPastAWholeTurn()
        {
            // AutoCAD 2021 (l2p pc, p7): angles 300 and 100 give 41 5.04586 and 42 7.95939 (the presenter folded it to 1.6762);
            // 0 and 400, 0 and 0.95004; 10 and 10 a whole turn, 0.285831 to 6.56902. entget gives the 42 as stored.
            CadDocument document = Blank();
            CadCommandSession session = Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "300", "100");
            Assert.Equal(5.04586, Last(document).StartParam, 4);
            Assert.Equal(7.95939, Last(document).EndParam, 4);
            Assert.Equal("7.95939", Value(session, "(cdr (assoc 42 (entget (entlast))))"));

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "0", "400");
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(0.95004, Last(document).EndParam, 4);

            session = Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "10", "10");
            Assert.Equal(0.285831, Last(document).StartParam, 4);
            Assert.Equal(6.56902, Last(document).EndParam, 4);
            Assert.Equal("6.56902", Value(session, "(cdr (assoc 42 (entget (entlast))))"));
        }

        [Fact]
        public void EntmakeOfAnEllipse_WithAParameterOutsideNoughtToTwoPi_GivesNil()
        {
            // AutoCAD 2021 (l2p pc): (41 5.0, 42 8.0), (41 1.0, 42 20.0) and (41 -1.0, 42 1.0) are all nil and make nothing; the
            // presenter made all three. The bounds themselves are not measured: this row stays away from them.
            CadDocument document = Blank();
            CadCommandSession session = Typed(document);
            const string head = "(entmake '((0 . \"ELLIPSE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) (40 . 0.5) ";

            Assert.Equal("nil", Value(session, head + "(41 . 5.0) (42 . 8.0)))"));
            Assert.Equal("nil", Value(session, head + "(41 . 1.0) (42 . 20.0)))"));
            Assert.Equal("nil", Value(session, head + "(41 . -1.0) (42 . 1.0)))"));
            Assert.Empty(All<DxfEllipse>(document.ModelSpace));

            Assert.NotEqual("nil", Value(session, head + "(41 . 1.0) (42 . 2.0)))"));
            Assert.Single(All<DxfEllipse>(document.ModelSpace));
        }

        #endregion

        #region DIMBLK (probe pb)

        [Fact]
        public void SetvarOfDimblkWithAnEmptyName_IsRefused()
        {
            // AutoCAD 2021 (l2p pb): (setvar "DIMBLK" "dot") then (setvar "DIMBLK" "") gives "; error: AutoCAD variable setting
            // rejected: "DIMBLK" """ and DIMBLK stays "Dot"; the presenter took it as the default arrowhead.
            CadCommandSession session = Typed(Blank());
            Value(session, "(setvar \"DIMBLK\" \"dot\")");

            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"DIMBLK\" \"\"",
                Value(session, "(setvar \"DIMBLK\" \"\")")
            );
            Assert.Equal("\"Dot\"", Value(session, "(getvar \"DIMBLK\")"));
            Assert.Equal("\".\"", Value(session, "(setvar \"DIMBLK\" \".\")"));
            Assert.Equal("\"\"", Value(session, "(getvar \"DIMBLK\")"));
        }

        [Fact]
        public void SetvarOfDimblkWithAnUnknownBlock_SaysTheBlockIsNotDefined()
        {
            // AutoCAD 2021 (l2p pb): (setvar "DIMBLK" "nosuch") writes 'Block "nosuch" not defined' on a line of its own and then
            // "; error: AutoCAD variable setting rejected: "DIMBLK" "nosuch"".
            CadCommandSession session = Typed(Blank());

            Assert.Equal(
                "Block \"nosuch\" not defined\n; error: AutoCAD variable setting rejected: \"DIMBLK\" \"nosuch\"",
                Value(session, "(setvar \"DIMBLK\" \"nosuch\")")
            );
        }

        [Fact]
        public void TheArrowheadNames_ReadWithoutTheirUnderscore_InAnyCase()
        {
            // AutoCAD 2021 (l2p pb): (setvar "DIMBLK" "_None") gives "_None" and (getvar "DIMBLK") is "None"; "none", "_none"
            // alike; "_DOT" and "dot" read back "Dot"; the block records _None and _Dot are made. Already so before this part.
            CadDocument document = Blank();
            CadCommandSession session = Typed(document);

            Assert.Equal("\"_None\"", Value(session, "(setvar \"DIMBLK\" \"_None\")"));
            Assert.Equal("\"None\"", Value(session, "(getvar \"DIMBLK\")"));
            Value(session, "(setvar \"DIMBLK2\" \"_DOT\")");
            Assert.Equal("\"Dot\"", Value(session, "(getvar \"DIMBLK2\")"));
            Assert.True(document.Database.BlockTable.Has("_None"));
            Assert.True(document.Database.BlockTable.Has("_Dot"));
        }

        [Fact]
        public void ADimensionStyleSavedWithTheArrowheads_ListsThemWithoutTheUnderscore()
        {
            // AutoCAD 2021 (l2p pb): after DIMBLK1 None and DIMBLK2 _DOT, -DIMSTYLE S GHDS gives (tblsearch "DIMSTYLE" "GHDS") with
            // (6 . "None") (7 . "None") (the second was set to None again in the probe); the presenter gave "_None". A style that
            // names _Dot lists "Dot" (ours, as the variable reads it).
            CadCommandSession session = Typed(Blank());
            Value(session, "(setvar \"DIMBLK1\" \"None\")");
            Value(session, "(setvar \"DIMBLK2\" \"_DOT\")");
            Run(session, "-DIMSTYLE", "S", "GHDS");

            Assert.Equal("\"None\"", Value(session, "(cdr (assoc 6 (tblsearch \"DIMSTYLE\" \"GHDS\")))"));
            Assert.Equal("\"Dot\"", Value(session, "(cdr (assoc 7 (tblsearch \"DIMSTYLE\" \"GHDS\")))"));
        }

        #endregion

        #region -STYLE (probes p3 and p6)

        [Fact]
        public void StyleTyped_OffersAnnotativeAtTheHeight_AndAsksTheTwoQuestionsOfYes()
        {
            // AutoCAD 2021 (l2fix p6): "Specify height of text or [Annotative] <0.0000>:", A asks "Create annotative text style
            // [Yes/No] <Yes>:", Y then "Match text orientation to layout? [Yes/No] <No>:", and the height again; the style has
            // the extended data (-3 ("AcadAnnotative" (1000 . "AnnotativeData") (1002 . "{") (1070 . 1) (1070 . 1) (1002 .
            // "}"))).
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHA", "txt");
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);

            Run(session, "A");
            Assert.Equal("Create annotative text style [Yes/No] <Yes>:", session.PromptText);

            Run(session, "Y");
            Assert.Equal("Match text orientation to layout? [Yes/No] <No>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);

            Run(session, Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.Equal("\"GHA\" is now the current text style.", session.History.Last());
            Assert.Contains(
                "(-3 (\"AcadAnnotative\" (1000 . \"AnnotativeData\") (1002 . \"{\") (1070 . 1) (1070 . 1) (1002 . \"}\")))",
                Value(session, "(entget (tblobjname \"STYLE\" \"GHA\") '(\"AcadAnnotative\"))"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void StyleTyped_AnnotativeNo_AsksTheHeightAgain_AndAddsNothing()
        {
            // AutoCAD 2021 (l2fix p6): A then N asks the height again and the style has no extended data.
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHB", "txt", "A", "N");
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);

            Run(session, Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.DoesNotContain(
                "AcadAnnotative",
                Value(session, "(entget (tblobjname \"STYLE\" \"GHB\") '(\"AcadAnnotative\"))"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void StyleTyped_AFontWithNoExtensionThatIsAnAutoCadShxFile_IsTheShxFile()
        {
            // AutoCAD 2021 (l2fix p3, p6): -STYLE GHT2 romans gives (3 . "romans.shx") and -STYLE GHB txt (3 . "txt.shx"); the
            // presenter took the TrueType files romans__.ttf and txt____.ttf it found in the Windows fonts folder. An SHX font
            // asks "Vertical? [Yes/No] <No>:" and an existing one offers its file, <romans.shx>, and says no "Full font name".
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHT2", "romans", "2.5", "0.8", "15", "N", "Y");
            Assert.Equal("Vertical? [Yes/No] <No>:", session.PromptText);

            Run(session, "N");
            Assert.Contains(
                "(3 . \"romans.shx\")",
                Value(session, "(tblsearch \"STYLE\" \"GHT2\")"),
                StringComparison.Ordinal
            );

            Run(session, "-STYLE", "GHB", "txt", "0", "1", "0", "N", "N", "N");
            Assert.Contains(
                "(3 . \"txt.shx\")",
                Value(session, "(tblsearch \"STYLE\" \"GHB\")"),
                StringComparison.Ordinal
            );

            Run(session, "-STYLE", "GHT2");
            Assert.Equal("Existing style.", session.History.Last());
            Assert.Equal("Specify font name or font filename (for SHX) <romans.shx>:", session.PromptText);
        }

        [Fact]
        public void StyleTyped_AFontOfNoKnownNameOrFile_KeepsItsNameAsAnShxFile()
        {
            // AutoCAD 2021 (l2p spec D1119): where the presenter has no file for an SHX font it keeps the name, written
            // name.shx, and draws it with its substitute.
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHT5", "nosuchfontname");
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);

            Run(session, "0", "1", "0", "N", "N", "N");
            Assert.Contains(
                "(3 . \"nosuchfontname.shx\")",
                Value(session, "(tblsearch \"STYLE\" \"GHT5\")"),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void StyleTyped_ATrueTypeFamilyOfTheMachine_StaysTrueType()
        {
            // AutoCAD 2021 (l2fix p3): -STYLE GHT Arial gives a TrueType style, "Existing style. Full font name = Arial" the next
            // time; a family that is none of AutoCAD's SHX names is not turned into one.
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHT6", "Arial", "0", "1", "0", "N", "N");

            Assert.DoesNotContain(
                ".shx",
                Value(session, "(cdr (assoc 3 (tblsearch \"STYLE\" \"GHT6\")))"),
                StringComparison.Ordinal
            );
            Run(session, "-STYLE", "GHT6");
            Assert.Equal("Existing style. Full font name = Arial", session.History.Last());
        }

        [Fact]
        public void StyleTyped_ANewStyle_StartsFromTheDefaults_NotFromTheCurrentStyle()
        {
            // AutoCAD 2021 (l2fix p3, p6): a new style made while the current one is Arial (p3) or 3 high (p6) offers <txt> as
            // its font and <0.0000> as its height.
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHT", "Arial", "3", "1", "0", "N", "N");
            Run(session, "-STYLE", "GHT7");

            Assert.Equal("New style.", session.History.Last());
            Assert.Equal("Specify font name or font filename (for SHX) <txt>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);
        }

        #endregion
    }
}
