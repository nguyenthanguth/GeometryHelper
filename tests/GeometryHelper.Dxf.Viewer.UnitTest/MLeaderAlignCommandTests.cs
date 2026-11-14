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
    /// MLEADERALIGN (MLA): the contents of multileaders aligned to the one picked along a direction, as AutoCAD 2021's
    /// (probed in its console: "Select multileaders:", "Current mode: Use current spacing", "Select multileader to align to or
    /// [Options]:", "Enter an option [Distribute/make leader segments Parallel/specify Spacing/Use current spacing] &lt;Use
    /// current spacing&gt;:"). Each content moves with its landing, the arrowheads staying where they are.
    /// </summary>
    public class MLeaderAlignCommandTests
    {
        /// <summary>Three multileaders with arrowheads at (0, 0), (30, 0) and (60, 0), their words landing at different heights.</summary>
        private static CadDocument Leaders(out string[] handles)
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Session(document);
            Run(session, "MLD", P(0, 0), P(10, 10), "ONE");
            Run(session, "MLD", P(30, 0), P(40, 25), "TWO");
            Run(session, "MLD", P(60, 0), P(75, 5), "THREE");
            handles = All<DxfMLeader>(document.ModelSpace).Select(m => m.Handle).ToArray();
            Assert.Equal(3, handles.Length);
            return document;
        }

        private static GeoPoint3 Content(CadDocument document, string handle) =>
            Entity<DxfMLeader>(document, handle).TextLocation;

        /// <summary>
        /// The landing of a content as AutoCAD 2021 measures it (probed in its console): a landing gap before the words, the
        /// middle of their first line level with it. A direction is taken from here, not from the words' point.
        /// </summary>
        private static GeoPoint2 Landing(CadDocument document, string handle)
        {
            DxfMLeader mleader = Entity<DxfMLeader>(document, handle);
            GeoPoint3 words = mleader.TextLocation;
            return new GeoPoint2(words.X - mleader.LandingGap * mleader.Scale, words.Y - mleader.TextHeight / 2.0);
        }

        /// <summary>How far the box of a content reaches across: its words and a landing gap before and after them.</summary>
        private static double Reach(CadDocument document, string handle)
        {
            DxfMLeader mleader = Entity<DxfMLeader>(document, handle);
            return mleader.MText.ActualWidth + 2.0 * mleader.LandingGap * mleader.Scale;
        }

        [Fact]
        public void TheCommandAsksInAutoCadsWords()
        {
            CadDocument document = Leaders(out string[] handles);
            CadCommandSession session = Run(Session(document), "MLEADERALIGN");
            Assert.Equal("Select multileaders:", session.PromptText);

            Run(session, handles, Enter);
            Assert.Contains("Current mode: Use current spacing", session.History);
            Assert.Equal("Select multileader to align to or [Options]:", session.PromptText);

            Run(session, "O");
            Assert.Equal(
                "Enter an option [Distribute/make leader segments Parallel/specify Spacing/Use current spacing] <Use current spacing>:",
                session.PromptText
            );

            Run(session, "S");
            Assert.Equal("Specify spacing <0.000000>:", session.PromptText);
            Run(session, "20");
            Assert.Contains("Current mode: Spacing: 20.000000", session.History);
            Assert.Equal("Select multileader to align to or [Options]:", session.PromptText);
        }

        [Fact]
        public void TheAliasMla_StartsIt()
        {
            Assert.Equal("Select multileaders:", Run(Session(Leaders(out _)), "MLA").PromptText);
        }

        [Fact]
        public void UseCurrentSpacing_PutsTheContentsOnTheLineThroughTheOnePicked_KeepingTheirPlacesAlongIt()
        {
            CadDocument document = Leaders(out string[] handles);
            GeoPoint3 reference = Content(document, handles[0]);
            GeoPoint2 landing = Landing(document, handles[0]);
            GeoPoint3 two = Content(document, handles[1]);
            GeoPoint3 three = Content(document, handles[2]);
            GeoPoint3 arrow = Entity<DxfMLeader>(document, handles[1])
                .GetFirstVertex(Entity<DxfMLeader>(document, handles[1]).GetLeaderLineIndexes(0)[0]);

            CadCommandSession session = Run(
                Session(document),
                "MLA",
                handles,
                Enter,
                new[] { handles[0] },
                P(landing.X + 100, landing.Y)
            );

            Assert.False(session.IsRunning);
            Assert.Equal(reference.Y, Content(document, handles[1]).Y, 6);
            Assert.Equal(reference.Y, Content(document, handles[2]).Y, 6);
            Assert.Equal(two.X, Content(document, handles[1]).X, 6);
            Assert.Equal(three.X, Content(document, handles[2]).X, 6);
            Assert.Equal(reference.X, Content(document, handles[0]).X, 6);
            DxfMLeader moved = Entity<DxfMLeader>(document, handles[1]);
            Near(arrow, moved.GetFirstVertex(moved.GetLeaderLineIndexes(0)[0]));
            Assert.Equal("MLEADERALIGN", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(two.Y, Content(document, handles[1]).Y, 6);
        }

        [Fact]
        public void ADirectionStraightUp_PutsTheContentsOnAVerticalLine()
        {
            CadDocument document = Leaders(out string[] handles);
            GeoPoint3 reference = Content(document, handles[0]);
            GeoPoint2 landing = Landing(document, handles[0]);

            Run(Session(document), "MLA", handles, Enter, new[] { handles[0] }, P(landing.X, landing.Y + 50));

            Assert.Equal(reference.X, Content(document, handles[1]).X, 6);
            Assert.Equal(reference.X, Content(document, handles[2]).X, 6);
        }

        [Fact]
        public void SpecifySpacing_StacksTheContentsAlongTheLineAGapApart_TheOnePickedStaying()
        {
            // AutoCAD 2021, words 1.14, 0.06 and 2.34 wide (WWWWW, I, WWWWWWWWWW) in a style of a landing gap of 0.09, a spacing
            // of 20 along a direction to the right: the next landing stands 21.32 beyond the first (1.14 and a gap before and
            // after the words, and 20), the third 20.24 beyond that.
            CadDocument document = Leaders(out string[] handles);
            GeoPoint3 reference = Content(document, handles[0]);
            GeoPoint2 landing = Landing(document, handles[0]);

            Run(
                Session(document),
                "MLA",
                handles,
                Enter,
                "O",
                "S",
                "25",
                new[] { handles[0] },
                P(landing.X + 100, landing.Y)
            );

            Assert.Equal(reference.X, Content(document, handles[0]).X, 6);
            Assert.Equal(reference.X + 25.0 + Reach(document, handles[0]), Content(document, handles[1]).X, 6);
            Assert.Equal(
                reference.X + 50.0 + Reach(document, handles[0]) + Reach(document, handles[1]),
                Content(document, handles[2]).X,
                6
            );
            Assert.Equal(reference.Y, Content(document, handles[2]).Y, 6);
        }

        [Fact]
        public void SpecifySpacing_PutsTheOnePickedFirst_AndTheOthersFollowItInTheOrderTheyStandAlongTheDirection()
        {
            // AutoCAD 2021, the middle of three picked with a direction to the right: the first and the third both follow it,
            // the first standing before the third as they did; to the left, the third comes before the first.
            CadDocument document = Leaders(out string[] handles);
            GeoPoint2 landing = Landing(document, handles[1]);
            GeoPoint3 reference = Content(document, handles[1]);

            Run(
                Session(document),
                "MLA",
                handles,
                Enter,
                "O",
                "S",
                "20",
                new[] { handles[1] },
                P(landing.X + 100, landing.Y)
            );

            Assert.Equal(reference.X, Content(document, handles[1]).X, 6);
            Assert.Equal(reference.X + 20.0 + Reach(document, handles[1]), Content(document, handles[0]).X, 6);
            Assert.Equal(
                reference.X + 40.0 + Reach(document, handles[1]) + Reach(document, handles[0]),
                Content(document, handles[2]).X,
                6
            );
        }

        [Fact]
        public void SpecifySpacing_UpTheLine_StepsByTheHeightOfTheWordsAndTheSpacing()
        {
            // AutoCAD 2021, spacing 0 up the line: single lines of words 0.18 high stand 0.18 apart, flush.
            CadDocument document = Leaders(out string[] handles);
            GeoPoint2 landing = Landing(document, handles[0]);
            GeoPoint3 reference = Content(document, handles[0]);
            double height = Entity<DxfMLeader>(document, handles[0]).MText.ActualHeight;
            double step = height > 0.0 ? height : Entity<DxfMLeader>(document, handles[0]).TextHeight;

            Run(
                Session(document),
                "MLA",
                handles,
                Enter,
                "O",
                "S",
                "0",
                new[] { handles[0] },
                P(landing.X, landing.Y + 100)
            );

            // The one lowest on the page (the third, its landing at y = 5) follows the one picked, then the second (y = 25).
            Assert.Equal(reference.Y + step, Content(document, handles[2]).Y, 6);
            Assert.Equal(reference.Y + 2.0 * step, Content(document, handles[1]).Y, 6);
            Assert.Equal(reference.X, Content(document, handles[2]).X, 6);
        }

        [Fact]
        public void Distribute_SpacesTheContentsEvenlyBetweenTwoPoints_InTheOrderAlongThem()
        {
            CadDocument document = Leaders(out string[] handles);

            CadCommandSession session = Run(Session(document), "MLA", handles, Enter, "O", "D");
            Assert.Equal("Specify first point or [Options]:", session.PromptText);
            Run(session, P(0, 100));
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, P(100, 100));

            // AutoCAD 2021: the first landing on the first point, the last on the second (words 1.32, 0.24 and 2.52 across
            // their boxes at (50, 120) and (250, 120): the middle at 150.54, a gap of 99.22 each side).
            List<GeoPoint2> landings = handles.Select(h => Landing(document, h)).OrderBy(p => p.X).ToList();
            double[] reach = handles.OrderBy(h => Landing(document, h).X).Select(h => Reach(document, h)).ToArray();
            Assert.Equal(0.0, landings[0].X, 6);
            Assert.Equal(100.0, landings[2].X, 6);
            double gap = (100.0 - reach[0] - reach[1]) / 2.0;
            Assert.Equal(reach[0] + gap, landings[1].X, 6);

            foreach (string handle in handles)
            {
                Assert.Equal(100.0, Landing(document, handle).Y, 6);
            }
        }

        [Fact]
        public void MakeLeaderSegmentsParallel_TurnsEachLeaderAboutItsArrowhead_KeepingItsLength_AndTheContentGoesWithIt()
        {
            // AutoCAD 2021: arrows (10, 10), (70, 30) and (130, 10), landings (40, 40), (100, 80) and (160, 20); with the
            // first one picked (45 degrees) the second's leader of 58.31 ends at (111.2311, 71.2311), the third's of 31.62 at
            // (152.3607, 32.3607), each content moving with its end and the arrowheads staying.
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession made = Session(document);
            Run(made, "MLD", P(10, 10), P(40, 40), "ONE");
            Run(made, "MLD", P(70, 30), P(100, 80), "TWO");
            Run(made, "MLD", P(130, 10), P(160, 20), "THREE");
            string[] handles = All<DxfMLeader>(document.ModelSpace).Select(m => m.Handle).ToArray();
            DxfMLeader two = Entity<DxfMLeader>(document, handles[1]);
            DxfMLeader three = Entity<DxfMLeader>(document, handles[2]);
            GeoPoint3 twoWords = two.TextLocation;
            GeoPoint3 twoEnd = two.GetLastVertex(two.GetLeaderLineIndexes(two.GetLeaderIndexes()[0])[0]);

            CadCommandSession session = Run(Session(document), "MLA", handles, Enter, "O", "P");
            Assert.Equal("Select multileader to align to or [Options]:", session.PromptText);
            Assert.Contains("Current mode: Make leader lines parallel", session.History);
            Run(session, (object)new[] { handles[0] });

            Assert.False(session.IsRunning);
            int lineTwo = two.GetLeaderLineIndexes(two.GetLeaderIndexes()[0])[0];
            GeoPoint3 arrowTwo = two.GetFirstVertex(lineTwo);
            GeoPoint3 endTwo = two.GetLastVertex(lineTwo);
            Assert.Equal(70.0, arrowTwo.X, 6);
            Assert.Equal(30.0, arrowTwo.Y, 6);
            Assert.Equal(endTwo.X - arrowTwo.X, endTwo.Y - arrowTwo.Y, 6);
            Assert.Equal(
                Math.Sqrt(Math.Pow(twoEnd.X - 70.0, 2) + Math.Pow(twoEnd.Y - 30.0, 2)),
                Math.Sqrt(2.0) * (endTwo.X - arrowTwo.X),
                6
            );
            Assert.Equal(twoWords.X + (endTwo.X - twoEnd.X), two.TextLocation.X, 6);
            Assert.Equal(twoWords.Y + (endTwo.Y - twoEnd.Y), two.TextLocation.Y, 6);
            int lineThree = three.GetLeaderLineIndexes(three.GetLeaderIndexes()[0])[0];
            GeoPoint3 endThree = three.GetLastVertex(lineThree);
            Assert.Equal(endThree.X - 130.0, endThree.Y - 10.0, 6);
            Assert.Equal("MLEADERALIGN", document.NextUndo.Name);
        }

        [Fact]
        public void ObjectsThatAreNotMultileaders_AreFilteredOut_AndTheCommandEnds()
        {
            CadDocument plan = Plan();

            CadCommandSession session = Run(Session(plan), "MLA", new[] { "A1", "A3" }, Enter);

            Assert.Contains("2 were filtered out.", session.History);
            Assert.False(session.IsRunning);
            Assert.False(plan.CanUndo);
        }
    }
}
