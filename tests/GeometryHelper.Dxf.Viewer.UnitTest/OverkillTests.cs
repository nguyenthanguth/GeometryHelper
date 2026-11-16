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
    /// OVERKILL and -OVERKILL against what AutoCAD 2021's console did (probed through its standard input over the same
    /// drawings): what is a duplicate and what an overlap, which object stays, how the tolerance, the ignored properties and
    /// the polyline options change it, and the words it writes.
    /// </summary>
    public class OverkillTests
    {
        private const string Duplicates0 = "0 duplicate(s) deleted";

        private static CadDocument Drawing(params string[] entities) =>
            CadDocument.From(TestDrawings.Drawing(string.Empty, string.Empty, entities), "overkill.dxf");

        private static string Point(double x, double y, string handle) =>
            TestDrawings.Entity("POINT", "0", 256, handle, "10", x, "20", y, "30", 0.0);

        private static string[] Handles(int count) => Enumerable.Range(1, count).Select(i => "B" + i).ToArray();

        /// <summary>Runs -OVERKILL over every entity (B1 to Bn) with the options typed and Enter at the end.</summary>
        private static CadCommandSession Kill(CadDocument document, int count, params object[] options)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "-OVERKILL", Handles(count), Enter);
            Run(session, options);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            return session;
        }

        private static List<string> Lines(CadDocument document) =>
            All<DxfLine>(document.ModelSpace).Select(l => Text(l.StartPoint) + ">" + Text(l.EndPoint)).ToList();

        private static string Text(GeoPoint3 p) =>
            Num(p.X) + "," + Num(p.Y) + (Math.Abs(p.Z) > 1E-9 ? "," + Num(p.Z) : string.Empty);

        private static string Num(double value) =>
            (Math.Round(value, 4) + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

        private static List<string> Arcs(CadDocument document) =>
            All<DxfArc>(document.ModelSpace)
                .Select(a =>
                    "r"
                    + Num(a.Radius)
                    + " "
                    + Num(a.StartAngle * 180.0 / Math.PI)
                    + ".."
                    + Num(a.EndAngle * 180.0 / Math.PI)
                )
                .ToList();

        private static List<string> Plines(CadDocument document) =>
            All<DxfPolyline>(document.ModelSpace)
                .Select(p =>
                    (p.Closed ? "closed " : string.Empty)
                    + string.Join(
                        " ",
                        Enumerable
                            .Range(0, p.NumberOfVertices)
                            .Select(i => p.GetPoint2dAt(i))
                            .Select(v => "(" + Num(v.X) + "," + Num(v.Y) + ")")
                    )
                )
                .ToList();

        [Fact]
        public void TheDefaultRun_DeletesTheDuplicate_CombinesTheOverlapAndTheEndToEnd_AsAutoCadWrote()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, handle: "B2"),
                TestDrawings.Line(50, 0, 150, 0, handle: "B3"),
                TestDrawings.Line(200, 0, 250, 0, handle: "B4"),
                TestDrawings.Line(250, 0, 300, 0, handle: "B5")
            );
            string start = document.Database.ToText();

            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "-OVERKILL", Handles(5), Enter);
            Assert.Contains(
                "Current settings: Tolerance=0.000001, Ignore=None, Optimize polylines=Yes, Combine partial overlap=Yes, Combine end-to-end=Yes",
                session.History
            );
            Assert.Equal(
                "Enter an option to change [Done/Ignore/tOlerance/optimize Plines/combine parTial overlap/combine Endtoend/Associativity] <done>:",
                session.PromptText
            );

            Run(session, Enter);

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Contains("2 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "0,0>150,0", "200,0>300,0" }, Lines(document));
            Assert.Equal("-OVERKILL", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void OfTwoDuplicatesTheLaterStays_AReversedLineIsTheSameLine_AndOneWithinAnotherIsAnOverlap()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(20, 0, 60, 0, handle: "B2"),
                TestDrawings.Line(0, 10, 100, 10, handle: "B3"),
                TestDrawings.Line(100, 10, 0, 10, handle: "B4")
            );

            CadCommandSession session = Kill(document, 4);

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Contains("1 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "0,0>100,0", "100,10>0,10" }, Lines(document));
        }

        [Fact]
        public void WithPartialOverlapNo_NothingButDuplicatesGoes_AndTheOverlapLineIsNotWritten()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(50, 0, 150, 0, handle: "B2"),
                TestDrawings.Line(200, 0, 300, 0, handle: "B3"),
                TestDrawings.Line(220, 0, 240, 0, handle: "B4")
            );

            CadCommandSession session = Kill(document, 4, "T", "N");

            Assert.Contains(Duplicates0, session.History);
            Assert.Contains("0 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(4, Lines(document).Count);

            document.Undo();
            Run(session, "-OVERKILL", Handles(4), Enter, "E", "N", Enter);

            Assert.Equal(1, session.History.Count(h => h == "0 overlapping object(s) or segment(s) deleted"));
            Assert.Contains(
                "Current settings: Tolerance=0.000001, Ignore=None, Optimize polylines=Yes, Combine partial overlap=No, Combine end-to-end=No",
                session.History
            );
        }

        [Fact]
        public void EndToEndNo_LeavesLinesThatMeet_AndALineAcrossAGapIsNeverJoined()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 50, 0, handle: "B1"),
                TestDrawings.Line(50, 0, 100, 0, handle: "B2"),
                TestDrawings.Line(0, 5, 50, 5, handle: "B3"),
                TestDrawings.Line(60, 5, 100, 5, handle: "B4")
            );

            Kill(document, 4, "E", "N");
            Assert.Equal(4, Lines(document).Count);

            document.Undo();
            Kill(document, 4, "E", "Y");
            Assert.Equal(new[] { "0,0>100,0", "0,5>50,5", "60,5>100,5" }, Lines(document));
        }

        [Fact]
        public void TheToleranceIsHalfTheDistanceTwoPointsMayBeApart()
        {
            CadDocument near = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(0.03, 0.04, 100, 0.04, handle: "B2")
            );
            CadCommandSession session = Kill(near, 2, "O", "0.1");
            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Equal(new[] { "0.03,0.04>100,0.04" }, Lines(near));
            Assert.Equal(0.1, session.Overkill.Tolerance);

            CadDocument far = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(0, 0.05, 100, 0.05, handle: "B2")
            );
            Kill(far, 2, "O", "0.1");
            Assert.Equal(2, Lines(far).Count);

            CadDocument gap = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(100.04, 0, 200, 0, handle: "B2")
            );
            Kill(gap, 2, "O", "0.1");
            Assert.Equal(new[] { "0,0>200,0" }, Lines(gap));
        }

        [Fact]
        public void LinesOfOtherColoursAreNotDuplicates_UntilIgnoreColor_AndTheSettingStaysForTheNextRun()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, color: 1, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, color: 5, handle: "B2")
            );

            CadCommandSession session = Kill(document, 2);
            Assert.Equal(2, Lines(document).Count);

            Run(session, "-OVERKILL", Handles(2), Enter, "I");
            Assert.Contains("Specify properties to ignore when comparing objects...", session.History);
            Run(session, "C", Enter);
            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Equal(5, All<DxfLine>(document.ModelSpace).Single().Color.ColorIndex);

            Run(session, "-OVERKILL", Handles(2), Enter);
            Assert.Contains(
                "Current settings: Tolerance=0.000001, Ignore=Color, Optimize polylines=Yes, Combine partial overlap=Yes, Combine end-to-end=Yes",
                session.History
            );
        }

        [Fact]
        public void IgnoreAll_TakesLinesOfEveryLayerAndColourForOne()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, color: 1, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, color: 5, handle: "B2"),
                TestDrawings.Line(0, 10, 100, 10, handle: "B3"),
                TestDrawings.Line(0, 10, 100, 10, color: 7, handle: "B4")
            );

            CadCommandSession session = Kill(document, 4, "I", "A");

            Assert.Contains("2 duplicate(s) deleted", session.History);
            Assert.Equal(2, Lines(document).Count);
        }

        [Fact]
        public void Arcs_AreDuplicatesWhenTheSame_CombineWhenTheyOverlapOrMeet_AndAnArcWithinAnotherGoes()
        {
            CadDocument document = Drawing(
                TestDrawings.Arc(0, 0, 50, 0, 90, handle: "B1"),
                TestDrawings.Arc(0, 0, 50, 0, 90, handle: "B2"),
                TestDrawings.Arc(0, 0, 50, 45, 135, handle: "B3"),
                TestDrawings.Arc(0, 0, 50, 60, 80, handle: "B4"),
                TestDrawings.Arc(0, 0, 50, 200, 250, handle: "B5"),
                TestDrawings.Arc(0, 0, 50, 250, 300, handle: "B6"),
                TestDrawings.Arc(0, 0, 60, 0, 90, handle: "B7")
            );

            CadCommandSession session = Kill(document, 7);

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Contains("3 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "r50 0..135", "r50 200..300", "r60 0..90" }, Arcs(document).OrderBy(a => a).ToArray());
        }

        [Fact]
        public void ACircleHoldsTheArcsOfItsOwnCircle_AndTwoTheSameCirclesAreDuplicates()
        {
            CadDocument document = Drawing(
                TestDrawings.Circle(0, 0, 50, handle: "B1"),
                TestDrawings.Circle(0, 0, 50, handle: "B2"),
                TestDrawings.Circle(0, 0, 40, handle: "B3"),
                TestDrawings.Arc(0, 0, 50, 0, 90, handle: "B4"),
                TestDrawings.Arc(0, 0, 40, 90, 360, handle: "B5")
            );

            CadCommandSession session = Kill(document, 5);

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Contains("2 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(2, All<DxfCircle>(document.ModelSpace).Count);
            Assert.Empty(All<DxfArc>(document.ModelSpace));
        }

        [Fact]
        public void ArcsOverlappingAcrossZeroDegrees_BecomeOne_AndArcsThatMakeAWholeCircleBecomeACircle()
        {
            CadDocument across = Drawing(
                TestDrawings.Arc(0, 0, 50, 300, 40, handle: "B1"),
                TestDrawings.Arc(0, 0, 50, 20, 100, handle: "B2")
            );
            Kill(across, 2);
            Assert.Equal(new[] { "r50 300..100" }, Arcs(across));

            CadDocument whole = Drawing(
                TestDrawings.Arc(0, 0, 50, 0, 200, handle: "B1"),
                TestDrawings.Arc(0, 0, 50, 150, 10, handle: "B2")
            );
            CadCommandSession session = Kill(whole, 2);
            Assert.Contains("1 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Empty(All<DxfArc>(whole.ModelSpace));
            Assert.Equal(50.0, Assert.Single(All<DxfCircle>(whole.ModelSpace)).Radius, 9);
        }

        [Fact]
        public void PointsOfTheSamePlace_AreDuplicates()
        {
            CadDocument document = Drawing(Point(5, 5, "B1"), Point(5, 5, "B2"), Point(6, 5, "B3"));

            CadCommandSession session = Kill(document, 3);

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Equal(2, All<DxfDBPoint>(document.ModelSpace).Count);
        }

        [Fact]
        public void TwoPolylinesOfTheSameVerticesAreDuplicates_EvenReversed_AndTheLaterStays()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B2", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B3", false, 100, 100, 0, 100, 0, 0, 0, 0, 0)
            );

            CadCommandSession session = Kill(document, 3);

            Assert.Contains("2 duplicate(s) deleted", session.History);
            Assert.Equal(new[] { "(100,100) (100,0) (0,0)" }, Plines(document));
        }

        [Fact]
        public void ALineOnASegmentOfAPolyline_IsDeletedAndThePolylineKeptWhole()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Line(0, 0, 100, 0, handle: "B2"),
                TestDrawings.Line(100, 0, 100, 50, handle: "B3")
            );

            CadCommandSession session = Kill(document, 3);

            Assert.Contains("2 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Empty(Lines(document));
            Assert.Equal(new[] { "(0,0) (100,0) (100,100)" }, Plines(document));
        }

        [Fact]
        public void TwoPolylinesThatOverlapOnASegment_LoseIt_AndALineOfTheWholeTakesItsPlace()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B2", false, 50, 0, 0, 150, 0, 0, 150, 50, 0)
            );

            CadCommandSession session = Kill(document, 2);

            Assert.Contains("1 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "(100,0) (100,100)", "(150,0) (150,50)" }, Plines(document));
            Assert.Equal(new[] { "0,0>150,0" }, Lines(document));
        }

        [Fact]
        public void APolylineLosesItsRepeatedVerticesAndJoinsItsCollinearSegments_InPlace()
        {
            CadDocument repeated = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 0, 0, 200, 0, 0)
            );
            CadCommandSession session = Kill(repeated, 1);
            Assert.Contains("2 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "(0,0) (200,0)" }, Plines(repeated));

            CadDocument collinear = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 50, 0, 0, 100, 0, 0, 100, 50, 0)
            );
            session = Kill(collinear, 1);
            Assert.Contains("1 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "(0,0) (100,0) (100,50)" }, Plines(collinear));
        }

        [Fact]
        public void APolylineThatDoublesBackOnItself_IsCutInTwo()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 50, 0, 0, 50, 50, 0)
            );

            CadCommandSession session = Kill(document, 1);

            Assert.Contains("1 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "(0,0) (100,0)", "(50,0) (50,50)" }, Plines(document).OrderBy(p => p).ToArray());
        }

        [Fact]
        public void BreakPolylinesNo_KeepsThePolylinesWhole_ThoughRepeatedVerticesStillGo()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B2", false, 50, 0, 0, 150, 0, 0, 150, 50, 0)
            );

            CadCommandSession session = Kill(document, 2, "P", "B", "N", "Y");

            Assert.Contains("0 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(new[] { "(0,0) (100,0) (100,100)", "(50,0) (150,0) (150,50)" }, Plines(document));
            Assert.True(session.Overkill.DoNotBreakPolylines);

            CadDocument dupe = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 0, 0, 200, 0, 0)
            );
            Kill(dupe, 1);
            Assert.Equal(new[] { "(0,0) (200,0)" }, Plines(dupe));
        }

        [Fact]
        public void OptimizeNo_TakesPolylinesAsWholesOnly_SoDuplicatesGoAndOverlapsStay()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B2", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                TestDrawings.Polyline("0", "B3", false, 50, 0, 0, 150, 0, 0, 150, 50, 0),
                TestDrawings.Polyline("0", "B4", false, 0, 0, 0, 100, 0, 0, 100, 0, 0, 200, 0, 0)
            );

            CadCommandSession session = Kill(document, 4, "P", "N");

            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Contains("0 overlapping object(s) or segment(s) deleted", session.History);
            Assert.Equal(3, Plines(document).Count);
        }

        [Fact]
        public void ALineMeetingAPolylinesSegmentEndToEnd_TakesItAndTheLineGrows()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 50, 0, handle: "B1"),
                TestDrawings.Polyline("0", "B2", false, 50, 0, 0, 100, 0, 0, 100, 100, 0)
            );

            Kill(document, 2);

            Assert.Equal(new[] { "0,0>100,0" }, Lines(document));
            Assert.Equal(new[] { "(100,0) (100,100)" }, Plines(document));
        }

        [Fact]
        public void TwentyThousandLinesOfThemTenThousandDuplicates_AreSortedOutInSeconds()
        {
            var lines = new List<string>();

            for (int i = 0; i < 10000; i++)
            {
                lines.Add(TestDrawings.Line(0, i * 3, 100, i * 3, handle: "B" + (2 * i + 1)));
                lines.Add(TestDrawings.Line(100, i * 3, 0, i * 3, handle: "B" + (2 * i + 2)));
            }

            CadDocument document = Drawing(lines.ToArray());
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "-OVERKILL", Handles(20000), Enter);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Run(session, Enter);
            clock.Stop();

            Assert.Contains("10000 duplicate(s) deleted", session.History);
            Assert.Equal(10000, Lines(document).Count);
            Assert.True(clock.Elapsed.TotalSeconds < 30.0, "It took " + clock.Elapsed.TotalSeconds + " s.");
        }

        [Fact]
        public void TheOptionsAreAskedAsAutoCadAsksThem()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"));
            CadCommandSession session = Session(document);
            Run(session, "-OVERKILL", Handles(1), Enter);

            Run(session, "O");
            Assert.Equal("Specify tolerance for object comparison precision <0.000001>:", session.PromptText);
            Run(session, "x");
            Assert.Contains("Requires numeric value.", session.History);
            Run(session, Enter);

            Run(session, "T");
            Assert.Equal("Combine co-linear objects that partially overlap [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter, "E");
            Assert.Equal("Combine co-linear objects when aligned end to end [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter, "A");
            Assert.Equal("Maintain associative objects [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter, "P");
            Assert.Equal(
                "Optimize segments within plines [segment wiDth/Break polyline/Yes/No] <Yes>:",
                session.PromptText
            );
            Run(session, "B");
            Assert.Equal("Break polylines [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");
            Assert.Equal(
                "Optimize segments within plines [segment wiDth/Break polyline/Yes/No] <Yes>:",
                session.PromptText
            );
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.False(session.Overkill.DoNotBreakPolylines);
        }

        [Fact]
        public void TheOverkillCommand_WithNoWindow_AsksTheSameOptions_AndWithOneTakesTheDialogsSettings()
        {
            CadDocument plain = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, handle: "B2")
            );
            CadCommandSession session = Session(plain);
            Run(session, "OVERKILL", Handles(2), Enter);
            Assert.StartsWith("Enter an option to change", session.PromptText);
            Run(session, Esc);

            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, color: 1, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, color: 5, handle: "B2")
            );
            session = Session(document);
            CadOverkillRequest shown = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "OVERKILL")
                {
                    shown = (CadOverkillRequest)e.Payload;
                    shown.Settings.Ignore = CadOverkillIgnore.Color;
                    shown.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "OVERKILL", Handles(2), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(2, shown.Selected);
            Assert.Contains("1 duplicate(s) deleted", session.History);
            Assert.Single(Lines(document));
            Assert.Equal(CadOverkillIgnore.Color, session.Overkill.Ignore);
        }

        [Fact]
        public void TheDialogCancelled_ChangesNothing()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Line(0, 0, 100, 0, handle: "B2")
            );
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "OVERKILL")
                {
                    ((CadOverkillRequest)e.Payload).Settings.Tolerance = 5;
                    ((CadOverkillRequest)e.Payload).Action = CadDialogAction.Cancel;
                    e.Handled = true;
                }
            };

            Run(session, "OVERKILL", Handles(2), Enter);

            Assert.Equal(2, Lines(document).Count);
            Assert.Equal(1E-6, session.Overkill.Tolerance);
            Assert.False(document.CanUndo);
        }
    }
}
