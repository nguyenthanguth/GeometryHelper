using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PEDIT against AutoCAD 2021: the prompts and the entities are those AutoCAD's own console asked and stored for the same
    /// input (accoreconsole, the entities listed with entget), unless a test says otherwise.
    /// </summary>
    public class PeditTests
    {
        private const string Options = "Enter an option [Close/Join/Width/Edit vertex/Fit/Spline/Decurve/Ltype gen/Reverse/Undo]:";
        private const string ClosedOptions = "Enter an option [Open/Join/Width/Edit vertex/Fit/Spline/Decurve/Ltype gen/Reverse/Undo]:";
        private const string ManyOptions = "Enter an option [Close/Open/Join/Width/Fit/Spline/Decurve/Ltype gen/Reverse/Undo]:";
        private const string VertexOptions = "Enter a vertex editing option [Next/Previous/Break/Insert/Move/Regen/Straighten/Tangent/Width/eXit] <N>:";

        /// <summary>The open polyline (0, 0), (100, 0), (100, 50), (0, 80) drawn with PLINE.</summary>
        private static CadDocument Zigzag(out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), P(100, 50), P(0, 80), Enter);
            return document;
        }

        private static DxfPolyline Light(CadDocument document) => Assert.Single(All<DxfPolyline>(document.ModelSpace));

        private static DxfPolyline2d Heavy(CadDocument document) => Assert.Single(All<DxfPolyline2d>(document.ModelSpace));

        private static void Vertices(DxfPolyline polyline, params double[] xy)
        {
            Assert.Equal(xy.Length / 2, polyline.NumberOfVertices);

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Near(P(xy[2 * i], xy[2 * i + 1]), polyline.GetPoint2dAt(i), 1E-7);
            }
        }

        [Fact]
        public void ALinePicked_IsTurnedIntoAPolylineOfOneSegment_AfterAutoCadsQuestion()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter);
            string line = Assert.Single(All<DxfLine>(document.ModelSpace)).Handle;

            Run(session, "PE");
            Assert.Equal("Select polyline or [Multiple]:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Contains("Object selected is not a polyline", session.History);
            Assert.Equal("Do you want to turn it into one? <Y>", session.PromptText);
            Run(session, "maybe");
            Assert.Contains("Yes or No, please.", session.History);
            Run(session, Enter);
            Assert.Equal(Options, session.PromptText);
            Run(session, "X");

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            DxfPolyline polyline = Light(document);
            Assert.NotEqual(line, polyline.Handle);
            Vertices(polyline, 0, 0, 100, 0);
        }

        [Fact]
        public void AnArcPicked_BecomesAPolylineFromItsStartCounterClockwise_ACircleIsNoPolyline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "ARC", P(100, 0), P(150, 50), P(200, 0), "CIRCLE", P(0, 300), "10");

            Run(session, "PEDIT", P(10, 300));
            Assert.Contains("Object selected is not a polyline", session.History);
            Assert.Equal("Select polyline or [Multiple]:", session.PromptText);
            Run(session, P(150, 50), Enter, "X");

            // AutoCAD 2021: the vertex (200, 0) of bulge 1, then (100, 0).
            DxfPolyline polyline = Light(document);
            Vertices(polyline, 200, 0, 100, 0);
            Assert.Equal(1.0, polyline.GetBulgeAt(0), 12);
            Assert.Empty(All<DxfArc>(document.ModelSpace));
        }

        [Fact]
        public void CloseOpenAndWidth_ChangeThePromptAndEveryVertex()
        {
            CadDocument document = Zigzag(out CadCommandSession session);

            Run(session, "PEDIT", P(50, 0), "C");
            Assert.Equal(ClosedOptions, session.PromptText);
            Assert.True(Light(document).Closed);
            Run(session, "O", "W");
            Assert.Equal("Specify new width for all segments:", session.PromptText);
            Run(session, "2", "X");

            DxfPolyline polyline = Light(document);
            Assert.False(polyline.Closed);
            Assert.Equal(2.0, polyline.ConstantWidth);
            Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(2.0, polyline.GetEndWidthAt(i)));
        }

        [Fact]
        public void Fit_MakesThePolylineAPolylineOfTheCurve_DecurveALwpolylineAgainOfTheSameHandle()
        {
            CadDocument document = Zigzag(out CadCommandSession session);
            string handle = Light(document).Handle;

            Run(session, "PEDIT", P(50, 0), "F", "X");

            // AutoCAD 2021: POLYLINE flagged 2, the vertices of the frame with those of the curve (flag 1) after each, the
            // first and last arcs as AutoCAD's (bulges 0.28407904 and 0.22783779).
            DxfPolyline2d fitted = Heavy(document);
            Assert.Equal(handle, fitted.Handle);
            Assert.Equal(DxfPoly2dType.FitCurvePoly, fitted.PolyType);
            List<DxfVertex2d> vertices = fitted.ToList();
            Assert.Equal(7, vertices.Count);
            Assert.All(new[] { 1, 3, 5 }, i => Assert.Equal(DxfVertex2dType.CurveFitVertex, vertices[i].VertexType));
            Assert.Equal(0.28407904, vertices[0].Bulge, 8);
            Assert.Equal(0.22783779, vertices[4].Bulge, 8);
            Near(new GeoPoint3(57.20937271, 89.03124237, 0), vertices[5].Position, 1E-7);

            Run(session, "PEDIT", P(0, 0), "D", "X");
            DxfPolyline again = Light(document);
            Assert.Equal(handle, again.Handle);
            Vertices(again, 0, 0, 100, 0, 100, 50, 0, 80);
            Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(0.0, again.GetBulgeAt(i)));
        }

        [Fact]
        public void Spline_IsTheCubicOfEightSegmentsASpan_StoredAsAutoCadStoresIt()
        {
            CadDocument document = Zigzag(out CadCommandSession session);
            Run(session, "PEDIT", P(50, 0), "S", "X");

            // AutoCAD 2021, SPLINETYPE 6 and SPLINESEGS 8: the first vertex of the frame (flag 16), the 9 of the curve (flag 8),
            // then the rest of the frame; it stays a POLYLINE at the end of the command.
            DxfPolyline2d spline = Heavy(document);
            Assert.Equal(DxfPoly2dType.CubicSplinePoly, spline.PolyType);
            List<DxfVertex2d> vertices = spline.ToList();
            Assert.Equal(13, vertices.Count);
            double[] curve = { 0, 0, 32.8125, 2.20703125, 56.25, 8.28125, 70.3125, 17.40234375, 75, 28.75, 70.3125, 41.50390625, 56.25, 54.84375, 32.8125, 67.94921875, 0, 80 };
            Assert.Equal(DxfVertex2dType.SplineControlVertex, vertices[0].VertexType);

            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(DxfVertex2dType.SplineFitVertex, vertices[i + 1].VertexType);
                Near(new GeoPoint3(curve[2 * i], curve[2 * i + 1], 0), vertices[i + 1].Position, 1E-8);
            }

            Assert.All(vertices.Skip(10), v => Assert.Equal(DxfVertex2dType.SplineControlVertex, v.VertexType));
        }

        [Fact]
        public void LtypeGenAndReverse_AskAsAutoCadAsks()
        {
            CadDocument document = Zigzag(out CadCommandSession session);

            Run(session, "PEDIT", P(50, 0), "L");
            Assert.Equal("Enter polyline linetype generation option [ON/OFF] <Off>:", session.PromptText);
            Run(session, "ON", "R", "X");

            DxfPolyline polyline = Light(document);
            Assert.True(polyline.Plinegen);
            Vertices(polyline, 0, 80, 100, 50, 100, 0, 0, 0);
        }

        [Fact]
        public void Undo_TakesBackTheLastOption_AndTheWholeRunIsOneStep()
        {
            CadDocument document = Zigzag(out CadCommandSession session);

            Run(session, "PEDIT", P(50, 0), "U");
            Assert.Contains("Command has been completely undone.", session.History);
            Assert.Equal(Options, session.PromptText);

            Run(session, "C", "W", "3", "U");
            Assert.True(Light(document).Closed);
            Assert.Equal(0.0, Light(document).ConstantWidth);
            Run(session, "F", "U", "U");
            Assert.False(Light(document).Closed);
            Run(session, "R", "X");
            Vertices(Light(document), 0, 80, 100, 50, 100, 0, 0, 0);

            Assert.Equal("PEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Vertices(Light(document), 0, 0, 100, 0, 100, 50, 0, 80);
        }

        [Fact]
        public void EditVertex_MovesInsertsStraightensAndWidens_AsAutoCad2021Did()
        {
            // AutoCAD 2021, the closed polyline of PLINE 0,0 W 1 3 100,0 A 100,50 L W 0 0 0,80 C, edited by four runs of
            // PEDIT E: N N N N M -10,90; N I 120,30 M 130,30; N S G; I 50,-5.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            var made = new DxfPolyline();
            made.AddVertexAt(0, P(0, 0), 0, 1, 3);
            made.AddVertexAt(1, P(100, 0), 1, 3, 3);
            made.AddVertexAt(2, P(100, 50), 0, 0, 0);
            made.AddVertexAt(3, P(0, 80), 0, 0, 0);
            made.Closed = true;
            document.ApplyEdit("draw", null, s => s.Append(document.ModelSpace, made));

            Run(session, "PEDIT", P(50, 0), "E");
            Assert.Equal(VertexOptions, session.PromptText);
            Near(P(0, 0), session.Prompt.Mark.Value);
            Run(session, "N", "N", "N", "N");
            Near(P(0, 80), session.Prompt.Mark.Value);
            Run(session, "M");
            Assert.Equal("Specify new location for marked vertex:", session.PromptText);
            Run(session, P(-10, 90), "X", "X");

            Run(session, "PEDIT", P(50, 0), "E", "N", "I");
            Assert.Equal("Specify location for new vertex:", session.PromptText);
            Run(session, P(120, 30));
            Near(P(120, 30), session.Prompt.Mark.Value);
            Run(session, "M", P(130, 30), "X", "X");

            Run(session, "PEDIT", P(50, 0), "E", "N", "S");
            Assert.Equal("Enter an option [Next/Previous/Go/eXit] <N>:", session.PromptText);
            Run(session, "G", "X", "X");

            Run(session, "PEDIT", P(50, 0), "E", "I", P(50, -5), "X", "X");

            DxfPolyline polyline = Light(document);
            Assert.True(polyline.Closed);
            double[][] autocad =
            {
                new[] { 0.0, 0.0, 1, 3 }, new[] { 50.0, -5, 3, 3 }, new[] { 100.0, 0, 3, 3 }, new[] { 130.0, 30, 3, 3 },
                new[] { 100.0, 50, 0, 0 }, new[] { -10.0, 90, 0, 0 },
            };
            Assert.Equal(autocad.Length, polyline.NumberOfVertices);

            for (int i = 0; i < autocad.Length; i++)
            {
                Near(P(autocad[i][0], autocad[i][1]), polyline.GetPoint2dAt(i), 1E-9);
                Assert.Equal(autocad[i][2], polyline.GetStartWidthAt(i));
                Assert.Equal(autocad[i][3], polyline.GetEndWidthAt(i));
                Assert.Equal(0.0, polyline.GetBulgeAt(i));
            }
        }

        [Fact]
        public void EditVertexWidth_AsksTheWidthsOfTheNextSegment_TheEndOfferingTheStart()
        {
            CadDocument document = Zigzag(out CadCommandSession session);

            Run(session, "PEDIT", P(50, 0), "E", "N", "P");
            Assert.EndsWith("<P>:", session.PromptText);
            Run(session, "N", "W");
            Assert.Equal("Specify starting width for next segment <0.0000>:", session.PromptText);
            Run(session, "3");
            Assert.Equal("Specify ending width for next segment <3.0000>:", session.PromptText);
            Run(session, "5", "X", "X");

            DxfPolyline polyline = Light(document);
            Assert.Equal(3.0, polyline.GetStartWidthAt(1));
            Assert.Equal(5.0, polyline.GetEndWidthAt(1));
        }

        [Fact]
        public void EditVertexInAView_StartsAtTheFirstVertexShown_OrSaysThatNoneIs()
        {
            CadDocument document = Zigzag(out CadCommandSession session);
            var view = new CadView(1000, 500);
            view.ZoomTo(new DisplayBox(90, -10, 110, 10), 0.0);
            session.View = view;

            Run(session, "PEDIT", P(50, 0), "E");
            Near(P(100, 0), session.Prompt.Mark.Value);
            Run(session, "X", "X");

            view.ZoomTo(new DisplayBox(500, 500, 600, 600), 0.0);
            Run(session, "PEDIT", P(50, 0), "E");
            Assert.Contains("No visible vertices", session.History);
            Assert.Equal(Options, session.PromptText);
        }

        [Fact]
        public void BreakOfAClosedPolyline_LeavesTwoOpenPieces_AsAutoCadBreaksIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), P(100, 50), P(0, 80), "C");
            string handle = Light(document).Handle;

            Run(session, "PEDIT", P(50, 0), "E", "N", "B", "G");
            Assert.Contains("*Invalid*", session.History);
            Run(session, "B", "N", "G", "X", "X");

            List<DxfPolyline> pieces = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(2, pieces.Count);
            DxfPolyline first = Assert.Single(pieces, p => p.Handle == handle);
            DxfPolyline second = Assert.Single(pieces, p => p.Handle != handle);
            Assert.False(first.Closed);
            Assert.False(second.Closed);
            Vertices(first, 0, 0, 100, 0);
            Vertices(second, 100, 50, 0, 80, 0, 0);
        }

        [Fact]
        public void Join_AddsTheSegmentsMeetingAnEnd_AndRefusesAClosedPolyline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), Enter, "LINE", P(100, 0), P(100, 50), Enter, "LINE", P(100, 50), P(0, 50), Enter, "LINE", P(0, 52), P(0, 100), Enter);

            Run(session, "PEDIT", P(50, 0), "J");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, "ALL", Enter);
            Assert.Contains("2 segments added to polyline", session.History);
            Run(session, "X");
            Vertices(Assert.Single(All<DxfPolyline>(document.ModelSpace)), 0, 0, 100, 0, 100, 50, 0, 50);
            Assert.Single(All<DxfLine>(document.ModelSpace));

            Run(session, "PEDIT", P(50, 0), "C", "J");
            Assert.Contains("Can't join to a closed polyline", session.History);
            Assert.Equal(ClosedOptions, session.PromptText);
        }

        [Fact]
        public void Multiple_ConvertsLinesWidensAndJoinsWithinTheFuzz_AsAutoCad2021Joined()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(103, 0), P(103, 50), Enter, "LINE", P(100, 50), P(0, 52), Enter);
            Run(session, "PLINE", P(200, 0), P(210, 0), P(210, 10), "C");

            Run(session, "PEDIT", "M", "ALL", Enter);
            Assert.Equal("Convert Lines, Arcs and Splines to polylines [Yes/No]? <Y>", session.PromptText);
            Run(session, Enter);
            Assert.Equal(ManyOptions, session.PromptText);
            Run(session, "W", "1.5", "J");
            Assert.Contains("Join Type = Extend", session.History);
            Assert.Equal("Enter fuzz distance or [Jointype] <0.0000>:", session.PromptText);
            Run(session, "J");
            Assert.Equal("Enter join type [Extend/Add/Both] <Extend>:", session.PromptText);
            Run(session, "A");
            Assert.Contains("Join Type = Add segment", session.History);
            Run(session, "J", "B");
            Assert.Contains("Join Type = Both (Extend or Add)", session.History);
            Run(session, "4");
            Assert.Contains("2 segments added to polyline", session.History);
            Run(session, "L");
            Assert.Equal("Enter polyline linetype generation option [ON/OFF]", session.PromptText);
            Run(session, "OFF", "X");

            // AutoCAD 2021: the three lines one polyline of (0, 0), (103, 0), (103, 49.94), (0, 52), each end run on to where
            // the lines meet; the closed polyline left as it was but for its width.
            List<DxfPolyline> polylines = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(2, polylines.Count);
            DxfPolyline joined = Assert.Single(polylines, p => !p.Closed);
            Vertices(joined, 0, 0, 103, 0, 103, 49.94, 0, 52);
            Assert.Equal(1.5, joined.GetStartWidthAt(0));
            Assert.Equal(1.5, Assert.Single(polylines, p => p.Closed).ConstantWidth);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void MultipleFitThenDecurve_LeavesPolylines_AsAutoCadLeavesThem()
        {
            CadDocument document = Zigzag(out CadCommandSession session);
            Run(session, "PLINE", P(200, 0), P(210, 0), P(210, 10), Enter);

            Run(session, "PEDIT", "M", "ALL", Enter, "F", "D", "X");

            // Multiple makes no LWPOLYLINE again; edited alone, the next PEDIT does.
            List<DxfPolyline2d> heavy = All<DxfPolyline2d>(document.ModelSpace);
            Assert.Equal(2, heavy.Count);
            Assert.All(heavy, p => Assert.Equal(DxfPoly2dType.SimplePoly, p.PolyType));
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));

            Run(session, "PEDIT", P(50, 0), "X");
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
            AssertReopens(document);
        }

        [Fact]
        public void PeditAcceptOne_TurnsALineIntoAPolylineWithoutAsking_AloneAndInMultiple()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PEDITACCEPT", "1", "LINE", P(0, 0), P(100, 0), Enter);

            // AutoCAD 2021 at PEDITACCEPT 1: "Select polyline or [Multiple]:", then the options at once.
            int before = session.History.Count;
            Run(session, "PEDIT", P(50, 0));
            Assert.Equal(Options, session.PromptText);
            Assert.DoesNotContain("Object selected is not a polyline", session.History.Skip(before));
            Run(session, Enter);
            Vertices(Light(document), 0, 0, 100, 0);

            Run(session, "LINE", P(0, 100), P(100, 100), Enter);
            Run(session, "PEDIT", "M", new[] { Assert.Single(All<DxfLine>(document.ModelSpace)).Handle }, Enter);
            Assert.Equal(ManyOptions, session.PromptText);
            Run(session, Enter);
            Assert.Equal(2, All<DxfPolyline>(document.ModelSpace).Count);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void PlineTypeNought_MakesOldStylePolylines_OfPlinePolygonDonutAndPedit_ButNotOfRectang()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINETYPE", "0");
            Run(session, "PLINE", P(0, 100), P(100, 100), "W", "2", "4", P(100, 200), Enter);

            // AutoCAD 2021 at PLINETYPE 0: a POLYLINE of (0, 100), (100, 100) of widths 2 and 4, (100, 200) of 4 and 4.
            DxfPolyline2d pline = Heavy(document);
            List<DxfVertex2d> vertices = pline.ToList();
            Assert.Equal(3, vertices.Count);
            Near(new GeoPoint3(100, 100, 0), vertices[1].Position);
            Assert.Equal(2.0, vertices[1].StartWidth);
            Assert.Equal(4.0, vertices[1].EndWidth);
            Assert.Equal(4.0, vertices[2].StartWidth);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));

            Run(session, "U");
            Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
            Run(session, "PLINE", P(0, 100), P(100, 100), P(100, 200), "U", Enter);
            Assert.Equal(2, Heavy(document).Count());
            Run(session, "ERASE", "ALL", Enter);

            // PEDIT of a line, POLYGON and DONUT make POLYLINEs too; RECTANG a LWPOLYLINE, as AutoCAD's.
            Run(session, "LINE", P(0, 300), P(100, 300), Enter, "PEDIT", P(50, 300), Enter, "X");
            Run(session, "POLYGON", "4", P(400, 50), "I", "40");
            Run(session, "DONUT", "10", "20", P(600, 50), Enter);
            Run(session, "RECTANG", P(200, 0), P(300, 100));
            List<DxfPolyline2d> heavy = All<DxfPolyline2d>(document.ModelSpace);
            Assert.Equal(3, heavy.Count);
            Assert.Equal(2, heavy.Count(p => p.Closed));
            DxfPolyline2d donut = heavy.Single(p => p.Count() == 2 && p.Closed);
            Assert.All(donut, v => Assert.Equal(1.0, v.Bulge));
            Assert.All(donut, v => Assert.Equal(5.0, v.StartWidth));
            Assert.True(Assert.Single(All<DxfPolyline>(document.ModelSpace)).Closed);
            AssertReopens(document);
        }

        [Fact]
        public void PlineType_DecidesWhetherAPolylineEditedAloneIsMadeALwpolylineAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINETYPE", "0", "PLINE", P(0, 100), P(100, 100), P(100, 200), Enter);
            string handle = Heavy(document).Handle;

            // AutoCAD 2021: at 0 the POLYLINE widened stays one; at 1, as at 2, it is a LWPOLYLINE of its handle.
            Run(session, "PEDIT", P(50, 100), "W", "1", Enter);
            Assert.Equal(1.0, Heavy(document).First().StartWidth);
            Run(session, "PLINETYPE", "1", "PEDIT", P(50, 100), "W", "2", Enter);
            Assert.Equal(handle, Light(document).Handle);
            Assert.Equal(2.0, Light(document).ConstantWidth);
            Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
        }

        /// <summary>A polyline of one arc drawn with PLINE: about a centre, from an angle on by an included angle, in degrees.</summary>
        private static void ArcPline(CadCommandSession session, double cx, double cy, double radius, double from, double to)
        {
            double a = from * System.Math.PI / 180.0;
            Run(session, "PLINE", P(cx + radius * System.Math.Cos(a), cy + radius * System.Math.Sin(a)), "A", "CE", P(cx, cy), "A",
                (to - from).ToString("R", System.Globalization.CultureInfo.InvariantCulture), Enter);
        }

        /// <summary>The two polylines drawn joined by Multiple at a fuzz (40 unless given), by the join type given; the polylines then left.</summary>
        private static List<DxfPolyline> FuzzJoined(CadDocument document, CadCommandSession session, string type, string fuzz = "40")
        {
            Run(session, "PEDIT", "M", "ALL", Enter, "J", "J", type, fuzz, "X");
            return All<DxfPolyline>(document.ModelSpace);
        }

        [Fact]
        public void FuzzJoinExtend_TakesEndsWhoseMeetingPointIsWithinTheFuzzOfBoth_HoweverFarApartTheEnds()
        {
            // AutoCAD 2021, the line from (0, 0) to (100, 0) and another, the ends 42.4 apart: at a fuzz of 40, and of 35, the
            // two run on to (130, 0), 30 from each; from (125, 45), 45 from the meeting point, and from (145, 30) they are not
            // joined; from (110, 5) to (210, 6) the lines meet far off, so Extend does not join them and Both adds a segment.
            string[][] cases =
            {
                new[] { "130", "30", "130", "100", "E", "40", "0,0 130,0 130,100" },
                new[] { "130", "30", "130", "100", "E", "35", "0,0 130,0 130,100" },
                new[] { "125", "45", "125", "100", "E", "40", null },
                new[] { "145", "30", "145", "100", "E", "40", null },
                new[] { "110", "5", "210", "6", "E", "40", null },
                new[] { "110", "5", "210", "6", "B", "40", "0,0 100,0 110,5 210,6" },
            };

            foreach (string[] probe in cases)
            {
                CadDocument document = Blank();
                CadCommandSession session = Session(document);
                Run(session, "PLINE", P(0, 0), P(100, 0), Enter, "PLINE", probe[0] + "," + probe[1], probe[2] + "," + probe[3], Enter);
                List<DxfPolyline> left = FuzzJoined(document, session, probe[4], probe[5]);
                string label = probe[0] + "," + probe[1] + " " + probe[4] + " " + probe[5];

                if (probe[6] == null)
                {
                    Assert.True(left.Count == 2, "Joined: " + label);
                    continue;
                }

                Assert.True(left.Count == 1, "Not joined: " + label);
                double[] xy = probe[6].Split(' ', ',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Vertices(left[0], xy);
            }

            // The arc about (0, 0) of radius 100 to 80 degrees and the line from (-30, 103), 47.6 from its end: they meet at
            // (0, 100), 17.4 and 30.1 from the two ends, joined at a fuzz of 40 but not of 30.
            CadDocument arcs = Blank();
            CadCommandSession run = Session(arcs);
            ArcPline(run, 0, 0, 100, 0, 80);
            Run(run, "PLINE", P(-30, 103), P(-100, 110), Enter);
            Assert.Equal(2, FuzzJoined(arcs, run, "E", "30").Count);
            DxfPolyline joined = Assert.Single(FuzzJoined(arcs, run, "E"));
            Vertices(joined, 100, 0, 0, 100, -100, 110);
            Bulges(joined, 0.4142135624, 0, 0);
        }

        private static void Bulges(DxfPolyline polyline, params double[] bulges)
        {
            for (int i = 0; i < bulges.Length; i++)
            {
                Assert.Equal(bulges[i], polyline.GetBulgeAt(i), 8);
            }
        }

        [Fact]
        public void FuzzJoinExtend_RunsAnArcOnOrBackRoundItsCircle_ToWhereTheLineMeetsIt()
        {
            // AutoCAD 2021: the arc about (0, 0) of radius 100 from 0 to 80 degrees and the line from (30, 110) to (60, 140)
            // meet at (18.3095, 98.3095), the arc cut back to a bulge of 0.36125437.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            ArcPline(session, 0, 0, 100, 0, 80);
            Run(session, "PLINE", P(30, 110), P(60, 140), Enter);
            DxfPolyline joined = Assert.Single(FuzzJoined(document, session, "E"));
            Vertices(joined, 100, 0, 18.3095189485, 98.3095189485, 60, 140);
            Bulges(joined, 0.3612543741, 0, 0);

            // The line from (10, 130) up: the arc run on to (10, 99.4987), a bulge of 0.38517039, by Both as by Extend.
            document = Blank();
            session = Session(document);
            ArcPline(session, 0, 0, 100, 0, 80);
            Run(session, "PLINE", P(10, 130), P(10, 200), Enter);
            joined = Assert.Single(FuzzJoined(document, session, "B"));
            Vertices(joined, 100, 0, 10, 99.4987437107, 10, 200);
            Bulges(joined, 0.3851703882, 0, 0);

            // The line from (-19.508, 101.507) on to (-78.762, 110.942) crosses the circle at (38.582, 92.257) and at
            // (-8.0205, 99.6778); AutoCAD takes the second, nearer the two ends together, though the first is nearer the arc's.
            document = Blank();
            session = Session(document);
            ArcPline(session, 0, 0, 100, 0, 80);
            Run(session, "PLINE", P(-19.508, 101.507), P(-78.762, 110.942), Enter);
            joined = Assert.Single(FuzzJoined(document, session, "E"));
            Vertices(joined, 100, 0, -8.0204615738, 99.6778420520, -78.762, 110.942);
            Bulges(joined, 0.4379305874, 0, 0);

            // The rest of the cases probed: each meeting point and bulge AutoCAD 2021 stored.
            var cases = new[]
            {
                new { From = P(40, 95), To = P(100, 95), Type = "E", Meet = P(31.2249899920, 95), Bulge = 0.3239798971 },
                new { From = P(-10, 120), To = P(-10, 200), Type = "E", Meet = P(-10, 99.4987437107), Bulge = 0.4438656912 },
                new { From = P(-30, 103), To = P(-100, 110), Type = "B", Meet = P(0, 100), Bulge = 0.4142135624 },
                new { From = P(41.758, 93.392), To = P(-17.255, 104.226), Type = "E", Meet = P(28.7325594892, 95.7832972141), Bulge = 0.3312126216 },
            };

            foreach (var probe in cases)
            {
                document = Blank();
                session = Session(document);
                ArcPline(session, 0, 0, 100, 0, 80);
                Run(session, "PLINE", probe.From, probe.To, Enter);
                List<DxfPolyline> left = FuzzJoined(document, session, probe.Type);
                Assert.True(left.Count == 1, "Not joined: the line from " + probe.From);
                joined = left[0];
                Vertices(joined, 100, 0, probe.Meet.X, probe.Meet.Y, probe.To.X, probe.To.Y);
                Bulges(joined, probe.Bulge, 0, 0);
            }
        }

        [Fact]
        public void FuzzJoinExtend_MeetsTwoArcsWhereTheirCirclesCross_AndALineBeforeAnArc()
        {
            // AutoCAD 2021: the arc about (50, 150) of radius 70 from -122.3 to -60 degrees, joined to the arc about (0, 0),
            // starts at (1.0155, 99.9948) with a bulge of 0.33658225, the first run on to 0.41124239.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            ArcPline(session, 0, 0, 100, 0, 80);
            ArcPline(session, 50, 150, 70, -122.3, -60);
            DxfPolyline joined = Assert.Single(FuzzJoined(document, session, "E"));
            Vertices(joined, 100, 0, 1.0154680285, 99.9948439905, 85, 89.3782217351);
            Bulges(joined, 0.4112423923, 0.3365822503, 0);

            // The line from (150, -60) to (100, -20), then the arc about (0, 0) from 5 to 80 degrees: the line cut back to
            // (98.2543, -18.6035), the arc run back on to there, a bulge of 0.41790644.
            document = Blank();
            session = Session(document);
            Run(session, "PLINE", P(150, -60), P(100, -20), Enter);
            ArcPline(session, 0, 0, 100, 5, 80);
            joined = Assert.Single(FuzzJoined(document, session, "E"));
            Vertices(joined, 150, -60, 98.2543201158, -18.6034560926, 17.3648177667, 98.4807753012);
            Bulges(joined, 0, 0.4179064351, 0);
        }

        [Fact]
        public void FuzzJoin_AnArcWhoseCircleNothingCrosses_IsNotExtended_ButBothAddsASegment()
        {
            // AutoCAD 2021: the arc about (0, 0) of radius 100 and the line from (0, 105) to (-60, 105): Extend "0 segments
            // added to polyline", Both a straight segment from the arc's end to the line, the arc as it was.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            ArcPline(session, 0, 0, 100, 0, 80);
            Run(session, "PLINE", P(0, 105), P(-60, 105), Enter);
            Assert.Equal(2, FuzzJoined(document, session, "E").Count);
            Assert.Contains("0 segments added to polyline", session.History);

            DxfPolyline joined = Assert.Single(FuzzJoined(document, session, "B"));
            Vertices(joined, 100, 0, 17.3648177667, 98.4807753012, 0, 105, -60, 105);
            Bulges(joined, 0.3639702343, 0, 0, 0);
        }
    }
}
