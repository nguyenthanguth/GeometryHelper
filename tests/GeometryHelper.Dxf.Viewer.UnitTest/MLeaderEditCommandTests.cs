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
        public void AnArrowheadOnTheFarSideOfArialWords_MakesALeaderOfItsOwn_LandedAgainstTheirRightEnd()
        {
            // AutoCAD 2021 (annot4's probe: an arrow far on the right of "HELLO" 9 high lands a leader of its own, its dogleg
            // against x, the gap past the ink's right end): the words are measured by the fonts the presenter reads, so that
            // arial.ttf's "B-12" 4 high, 11.1 wide (0.49948 for 0.18), has the new landing 2 + 8 past 33.2 + 11.1.
            CadDocument document = BlankWithMLeaderStyle();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");
            string handle = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            Assert.Equal(1, Entity<DxfMLeader>(document, handle).LeaderCount);

            Run(Session(document), "MLE", new[] { handle }, P(200, 0), Enter);

            DxfMLeader mleader = Entity<DxfMLeader>(document, handle);
            Assert.Equal(2, mleader.LeaderCount);
            int added = mleader.GetLeaderIndexes().Max();
            Assert.Equal(new GeoVector3(-1, 0, 0), mleader.GetDogleg(added));
            double width = 0.49948 / 0.18 * style.TextHeight;
            double right = mleader.MText.Location.X + width;
            int line = mleader.GetLeaderLineIndexes(added).Single();
            Assert.True(
                System.Math.Abs(mleader.GetLastVertex(line).X - (right + style.LandingGap + style.DoglegLength)) < 5E-3,
                $"landing {mleader.GetLastVertex(line).X}, the words end at {right}"
            );
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

        /// <summary>An annotative multileader of the words "B-12" shown at 1:50 and 1:100, as AutoCAD 2021's MLEADER and OBJECTSCALE make it.</summary>
        private static CadDocument TwoScales(out string handle)
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:" + d.ToString("0"),
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            CadMLeaderStyleEditor.Find(document.Database, CadMLeaderStyleEditor.Current(document.Database)).Annotative =
                DxfAnnotativeStates.True;
            document.Database.Cannoscale = "1:50";
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");
            handle = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            Run(Session(document), "-OBJECTSCALE", new[] { handle }, Enter, "A", "1:100", Enter);
            return document;
        }

        private static int Lines(CadDocument document) =>
            System.Text.RegularExpressions.Regex.Matches(document.Database.ToText(), "LEADER_LINE\\{").Count;

        [Fact]
        public void AnArrowheadAddedToAnAnnotativeMultileader_IsInTheContextOfEveryScale_AndUndoneWithThemAll()
        {
            // AutoCAD 2021 (probe GHAQ): MLEADEREDIT puts the new leader line in the record and in the context of each scale.
            CadDocument document = TwoScales(out string handle);
            Assert.Equal(3, Lines(document));

            Run(Session(document), "MLE", new[] { handle }, P(0, 40), Enter);

            Assert.Equal(2, Entity<DxfMLeader>(document, handle).LeaderLineCount);
            Assert.Equal(6, Lines(document));
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(3, Lines(document));
        }

        [Fact]
        public void AnArrowheadOnTheFarSideOfAnAnnotativeMultileader_LandsAtTheEndOfTheWordsAtEachScale()
        {
            // AutoCAD 2021 (annot4's probe, "HELLO" 0.18 high of arial.ttf, dogleg 0.36, gap 0.09, arrow (100, 100), landing
            // (150, 150), the arrowhead added far on the right): the landing 234.6589 at 1:50 and 319.3179 at 1:100, the right end
            // of the words (0.79318 wide for 0.18) and the gap and the dogleg past it.
            CadDocument document = BlankWithMLeaderStyle();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:" + d.ToString("0"),
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            style.TextHeight = 0.18;
            style.LandingGap = 0.09;
            style.DoglegLength = 0.36;
            style.Annotative = DxfAnnotativeStates.True;
            document.Database.Cannoscale = "1:50";
            Run(Session(document), "MLD", P(100, 100), P(150, 150), "HELLO");
            string handle = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            Run(Session(document), "-OBJECTSCALE", new[] { handle }, Enter, "A", "1:100", Enter);

            Run(Session(document), "MLE", new[] { handle }, P(400, 100), Enter);

            DxfMLeader mleader = Entity<DxfMLeader>(document, handle);
            Assert.Equal(2, mleader.LeaderCount);
            int added = mleader.GetLeaderIndexes().Max();
            int line = mleader.GetLeaderLineIndexes(added).Single();
            Assert.True(
                System.Math.Abs(mleader.GetLastVertex(line).X - 234.6589) < 5E-3,
                $"landing {mleader.GetLastVertex(line).X} at 1:50, AutoCAD 234.6589"
            );
            Assert.Matches(@"\n\s*10\n319\.3[0-2]\d*\n", document.Database.ToText());
            AssertReopens(document);
        }

        [Fact]
        public void ALeaderLineTakenFromAnAnnotativeMultileader_IsTakenFromTheContextOfEveryScale()
        {
            CadDocument document = TwoScales(out string handle);
            Run(Session(document), "MLE", new[] { handle }, P(0, 40), Enter);
            Assert.Equal(6, Lines(document));

            Run(Session(document), "MLE", new[] { handle }, "R", P(0, 40), Enter);

            Assert.Equal(1, Entity<DxfMLeader>(document, handle).LeaderLineCount);
            Assert.Equal(3, Lines(document));
            AssertReopens(document);
        }
    }
}
