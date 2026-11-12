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
            Assert.Equal("Specify spacing <0.0000>:", session.PromptText);
            Run(session, "20");
            Assert.Contains("Current mode: Specify spacing = 20.0000", session.History);
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
                P(reference.X + 100, reference.Y)
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

            Run(Session(document), "MLA", handles, Enter, new[] { handles[0] }, P(reference.X, reference.Y + 50));

            Assert.Equal(reference.X, Content(document, handles[1]).X, 6);
            Assert.Equal(reference.X, Content(document, handles[2]).X, 6);
        }

        [Fact]
        public void SpecifySpacing_StacksTheContentsAlongTheLineAGapApart_TheOnePickedStaying()
        {
            CadDocument document = Leaders(out string[] handles);
            GeoPoint3 reference = Content(document, handles[0]);

            Run(
                Session(document),
                "MLA",
                handles,
                Enter,
                "O",
                "S",
                "25",
                new[] { handles[0] },
                P(reference.X + 100, reference.Y)
            );

            Assert.Equal(reference.X, Content(document, handles[0]).X, 6);
            Assert.Equal(reference.X + 25.0, Content(document, handles[1]).X, 6);
            Assert.Equal(reference.X + 50.0, Content(document, handles[2]).X, 6);
            Assert.Equal(reference.Y, Content(document, handles[2]).Y, 6);
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

            double[] along = handles.Select(h => Content(document, h)).OrderBy(p => p.X).Select(p => p.X).ToArray();
            Assert.Equal(0.0, along[0], 6);
            Assert.Equal(50.0, along[1], 6);
            Assert.Equal(100.0, along[2], 6);
            foreach (string handle in handles)
            {
                Assert.Equal(100.0, Content(document, handle).Y, 6);
            }
        }

        [Fact]
        public void MakeLeaderSegmentsParallel_IsSaidNotToBeBuilt_AndNothingMoves()
        {
            CadDocument document = Leaders(out string[] handles);
            GeoPoint3 two = Content(document, handles[1]);

            CadCommandSession session = Run(Session(document), "MLA", handles, Enter, "O", "P");

            Assert.Contains(
                "Make leader segments parallel is not built here: the mode stays as it was.",
                session.History
            );
            Assert.Equal(two.Y, Content(document, handles[1]).Y, 6);
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
