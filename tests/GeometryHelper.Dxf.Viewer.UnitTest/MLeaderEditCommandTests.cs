using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLEADEREDIT (MLE): leader lines added to and taken from a multileader, as AutoCAD 2021's (probed in its console: "Select
    /// a multileader:", then "Specify leader arrowhead location or [Remove leaders]:", Enter ending it).
    /// </summary>
    public class MLeaderEditCommandTests
    {
        /// <summary>A multileader whose words "B-12" are met by one line from (0, 0).</summary>
        private static CadDocument OneLeader(out string handle)
        {
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");
            handle = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            return document;
        }

        [Fact]
        public void TheCommandAsksInAutoCadsWords()
        {
            CadDocument document = OneLeader(out string handle);
            CadCommandSession session = Run(Session(document), "MLE");
            Assert.Equal("Select a multileader:", session.PromptText);

            Run(session, (object)new[] { handle });
            Assert.Equal("Specify leader arrowhead location or [Remove leaders]:", session.PromptText);

            Run(session, "R");
            Assert.Equal("Select leader to remove or [Add leaders]:", session.PromptText);

            Run(session, "A");
            Assert.Equal("Specify leader arrowhead location or [Remove leaders]:", session.PromptText);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnArrowheadLocationGiven_AddsALeaderLineThere_AndTheCommandAsksTheNext_InOneUndoStep()
        {
            CadDocument document = OneLeader(out string handle);
            Assert.Equal(1, Entity<DxfMLeader>(document, handle).LeaderLineCount);

            CadCommandSession session = Run(Session(document), "MLEADEREDIT", new[] { handle }, P(0, 40), P(0, -30));

            DxfMLeader mleader = Entity<DxfMLeader>(document, handle);
            Assert.Equal(3, mleader.LeaderLineCount);
            Assert.Equal("Specify leader arrowhead location or [Remove leaders]:", session.PromptText);
            var arrows = mleader
                .GetLeaderIndexes()
                .SelectMany(mleader.GetLeaderLineIndexes)
                .Select(line => mleader.GetFirstVertex(line))
                .ToList();
            Assert.Contains(arrows, p => p.DistanceTo(new GeoPoint3(0, 40, 0)) < 1E-9);
            Assert.Contains(arrows, p => p.DistanceTo(new GeoPoint3(0, -30, 0)) < 1E-9);

            Run(session, Enter);
            Assert.Equal("MLEADEREDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(1, Entity<DxfMLeader>(document, handle).LeaderLineCount);
            AssertReopens(document);
        }

        [Fact]
        public void ALeaderLinePickedUnderRemoveLeaders_IsTakenAway_TheOthersKept()
        {
            CadDocument document = OneLeader(out string handle);
            Run(Session(document), "MLEADEREDIT", new[] { handle }, P(0, 40), Enter);
            DxfMLeader two = Entity<DxfMLeader>(document, handle);
            Assert.Equal(2, two.LeaderLineCount);

            // The second line's arrowhead stands at (0, 40); a pick on its first vertex takes that line.
            CadCommandSession session = Run(Session(document), "MLE", new[] { handle }, "R", P(0, 40));

            DxfMLeader after = Entity<DxfMLeader>(document, handle);
            Assert.Equal(1, after.LeaderLineCount);
            int line = after.GetLeaderIndexes().SelectMany(after.GetLeaderLineIndexes).Single();
            Assert.True(after.GetFirstVertex(line).DistanceTo(new GeoPoint3(0, 0, 0)) < 1E-9);
            Assert.Equal("Select leader to remove or [Add leaders]:", session.PromptText);
            Run(session, Enter);
            Assert.True(document.Undo());
            Assert.Equal(2, Entity<DxfMLeader>(document, handle).LeaderLineCount);
        }

        [Fact]
        public void APickFarFromEveryLeaderLine_RemovesNothing()
        {
            CadDocument document = OneLeader(out string handle);
            Run(Session(document), "MLEADEREDIT", new[] { handle }, P(0, 40), Enter);

            CadCommandSession session = Run(Session(document), "MLE", new[] { handle }, "R", P(500, 500));

            Assert.Equal(2, Entity<DxfMLeader>(document, handle).LeaderLineCount);
            Assert.Contains("No leader line there.", session.History);
            Assert.Equal("MLEADEREDIT", document.NextUndo.Name);
        }

        [Fact]
        public void AnObjectThatIsNotAMultileader_IsRefused()
        {
            CadDocument document = Plan();

            CadCommandSession session = Run(Session(document), "MLE", new[] { "A1" });

            Assert.Contains("Object selected is not a multileader.", session.History);
            Assert.False(session.IsRunning);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheLastLeaderLine_IsNotTakenAway()
        {
            CadDocument document = OneLeader(out string handle);

            CadCommandSession session = Run(Session(document), "MLE", new[] { handle }, "R", P(0, 0));

            Assert.Equal(1, Entity<DxfMLeader>(document, handle).LeaderLineCount);
            Assert.Contains("A multileader keeps at least one leader line.", session.History);
        }
    }
}
