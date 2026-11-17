using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// MLEADERALIGN's Distribute and specify Spacing on lines of every slope, make leader segments Parallel on a leader of
    /// several lines, and the leader lines that go to the other side of the content, as AutoCAD 2021's (probed with
    /// multileaders made through its .NET API and MLEADERALIGN run on them in its console: about 500 cases of Distribute,
    /// Spacing, Use current spacing and Parallel on text and block contents, the contents' places and leaders compared). The
    /// gap between two contents along the line is the distance the second can slide along it from the first until the two
    /// boxes just clear each other, not the sum of their reaches.
    /// </summary>
    public class MLeaderAlignGapsTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private static DxfBlockTableRecord Rectangle(CadDocument document, string name, double wide, double high)
        {
            var block = new DxfBlockTableRecord { Name = name };
            document.Database.BlockTable.Add(block);
            var corners = new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(wide, 0, 0),
                new GeoPoint3(wide, high, 0),
                new GeoPoint3(0, high, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                block.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            return block;
        }

        /// <summary>
        /// Three multileaders of blocks, a rectangle 16 by 2.5, one 4 by 1 and one 10 by 5, with their insertion points at
        /// (-40, 25), (50, 25) and (140, 25), each held by the left middle of its extents, as AutoCAD 2021 holds them.
        /// </summary>
        private static CadDocument Blocks(out DxfMLeader[] items)
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfBlockTableRecord[] blocks =
            {
                Rectangle(document, "R16", 16, 2.5),
                Rectangle(document, "R4", 4, 1),
                Rectangle(document, "R10", 10, 5),
            };
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            style.ContentType = DxfContentType.BlockContent;
            style.Block = blocks[0];
            CadCommandSession session = Session(document);
            Run(session, "MLD", P(-60, 20), P(-45, 25));
            Run(session, "MLD", P(60, 20), P(45, 25));
            Run(session, "MLD", P(150, 20), P(135, 25));
            List<DxfMLeader> made = All<DxfMLeader>(document.ModelSpace);
            Assert.Equal(3, made.Count);
            var places = new[] { new GeoPoint3(-40, 25, 0), new GeoPoint3(50, 25, 0), new GeoPoint3(140, 25, 0) };

            for (int i = 0; i < 3; i++)
            {
                made[i].BlockContent = blocks[i];
                made[i].BlockPosition = places[i];
            }

            items = made.ToArray();
            return document;
        }

        private static void AssertPlaces(DxfMLeader[] items, params double[] xy)
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(xy[2 * i], items[i].BlockPosition.X, 4);
                Assert.Equal(xy[2 * i + 1], items[i].BlockPosition.Y, 4);
            }
        }

        [Fact]
        public void Distribute_OnALineAt45Degrees_PutsTheFirstAndLastOnThePoints_AndTheMiddleWhereTheGapsAreEqual()
        {
            // AutoCAD 2021: blocks 16 by 2.5, 4 by 1 and 10 by 5 distributed from (0, 0) to (100, 100). The left middle of the
            // first is on (0, 0), of the last on (100, 100); the second just clears the first at 3.5355 along the line (the
            // height of the boxes decides, 2.5 / sin 45 degrees, not their widths) and the last clears the second at 1.4142:
            // the middle one stands at 99.2... along a line of 141.42, 49.375 across.
            CadDocument document = Blocks(out DxfMLeader[] items);

            Run(Session(document), "MLA", items.Select(i => i.Handle).ToArray(), Enter, "O", "D", P(0, 0), P(100, 100));

            AssertPlaces(items, 0, -1.25, 49.375, 48.875, 100, 97.5);
        }

        [Fact]
        public void Distribute_OnALevelLine_StepsByTheWidthsOfTheBoxes()
        {
            // AutoCAD 2021: the same blocks from (0, 0) to (200, 0): the second stands where the gaps are equal between the
            // boxes, 106 (16 + 4 of boxes and 180 of gaps) from the first.
            CadDocument document = Blocks(out DxfMLeader[] items);

            Run(Session(document), "MLA", items.Select(i => i.Handle).ToArray(), Enter, "O", "D", P(0, 0), P(200, 0));

            AssertPlaces(items, 0, -1.25, 106, -0.5, 200, -2.5);
        }

        [Fact]
        public void Distribute_OnALineRunningUpAndLeft_TakesTheWidthOfTheBoxThatComesAfter()
        {
            // AutoCAD 2021: from (0, 0) to (-100, 100) the blocks are taken in the order they stand along the line, the last
            // of them (the one at the right) first. The boxes slide towards the left, so one clears the box before it where
            // its own right edge is past that box's left: the widths that count are those of the boxes that come after.
            CadDocument document = Blocks(out DxfMLeader[] items);

            Run(
                Session(document),
                "MLA",
                items.Select(i => i.Handle).ToArray(),
                Enter,
                "O",
                "D",
                P(0, 0),
                P(-100, 100)
            );

            AssertPlaces(items, -100, 98.75, -50.625, 50.125, 0, -2.5);
        }

        [Fact]
        public void SpecifySpacing_UpAnInclinedLine_PutsTheNextBoxWhereItJustClearsThePreviousOneAndTheSpacingMore()
        {
            // AutoCAD 2021: spacing 5 along a line up to the right through the left middle of the 4 by 1 block (picked), the
            // others following it in the order they stand: the 16 by 2.5 one and then the 10 by 5 one, each 5 more than its
            // clearance along the line from the box before it.
            CadDocument document = Blocks(out DxfMLeader[] items);

            Run(
                Session(document),
                "MLA",
                items.Select(i => i.Handle).ToArray(),
                Enter,
                "O",
                "S",
                "5",
                new[] { items[1].Handle },
                P(110, 125)
            );

            AssertPlaces(items, 53.637241, 30.281759, 50, 25, 58.480513, 37.063517);
        }

        [Fact]
        public void SpecifySpacing_DownAnInclinedLine_FollowsTheSameClearancesTheOtherWayRound()
        {
            // AutoCAD 2021: the same blocks along a line up to the left (its direction taken from the middle one): the line
            // runs to the left, so the widths that count are the next box's.
            CadDocument document = Blocks(out DxfMLeader[] items);

            Run(
                Session(document),
                "MLA",
                items.Select(i => i.Handle).ToArray(),
                Enter,
                "O",
                "S",
                "5",
                new[] { items[1].Handle },
                P(-50, 75)
            );

            AssertPlaces(items, 27.401512, 35.436252, 50, 25, 39.458332, 28.218126);
        }

        /// <summary>A blank drawing with a multileader style whose words are measured in a font of the drawing's own.</summary>
        private CadDocument Measured()
        {
            CadDocument document = BlankWithMLeaderStyle();
            _folder.Write("test.shx", ShxFiles.Unifont());
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);
            document.Database.TextStyleTable["Standard"].FileName = "test.shx";
            return document;
        }

        private static DxfMLeader Make(CadDocument document, GeoPoint2 arrow, GeoPoint2 landing, string words)
        {
            int before = All<DxfMLeader>(document.ModelSpace).Count;
            Run(Session(document), "MLD", arrow, landing, words);
            List<DxfMLeader> all = All<DxfMLeader>(document.ModelSpace);
            Assert.Equal(before + 1, all.Count);
            return all[all.Count - 1];
        }

        private double Ink(DxfMLeader like, string words)
        {
            var database = new DxfDatabase("AC1027") { FontSearchPath = _folder.FullPath };
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            var probe = new DxfMText
            {
                Contents = words,
                TextHeight = like.MText.TextHeight,
                Location = new GeoPoint3(0, 0, 0),
            };
            database.ModelSpace.AppendEntity(probe);
            probe.TextStyleName = "GH_SHX";
            return probe.ActualWidth;
        }

        [Fact]
        public void ALeaderWhoseArrowheadsAreOnBothSidesOfTheBox_KeepsTheLineOnItsSide_AndSendsTheOtherToANewLeaderOnTheOther()
        {
            // AutoCAD 2021: SOD from arrowheads at (-30, 0) and (40, 30) to one landing on the left of the words, aligned up
            // the line x = 10.36: the middle of its box is at 17, so the line of -30 keeps the leader on the left (its dogleg
            // ending at 10.0, 0.36 short of the box) and the line of 40 goes to a new leader, the next index, on the right
            // (its dogleg against x, ending 0.36 past the box), the words held where they were.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(-30, 0), P(-20, 25), "SOD");
            int line = two.AddLeaderLine(two.GetLeaderIndexes()[0]);
            two.AddFirstVertex(line, new GeoPoint3(40, 30, 0));
            double ink = Ink(two, "SOD");
            double left = one.TextLocation.X - one.LandingGap * one.Scale;
            double gap = two.LandingGap * two.Scale;
            double dogleg = two.DoglegLength * two.Scale;
            Assert.Single(two.GetLeaderIndexes());
            Assert.Equal(2, two.GetLeaderLineIndexes(two.GetLeaderIndexes()[0]).Count);

            Run(Session(document), "MLA", new[] { one.Handle, two.Handle }, Enter, new[] { one.Handle }, P(left, 110));

            IReadOnlyList<int> leaders = two.GetLeaderIndexes();
            Assert.Equal(2, leaders.Count);
            Assert.Equal(1.0, two.GetDogleg(leaders[0]).X, 6);
            Assert.Equal(-1.0, two.GetDogleg(leaders[1]).X, 6);
            IReadOnlyList<int> keptLines = two.GetLeaderLineIndexes(leaders[0]);
            IReadOnlyList<int> sentLines = two.GetLeaderLineIndexes(leaders[1]);
            Assert.Single(keptLines);
            Assert.Single(sentLines);
            Assert.Equal(-30.0, two.GetFirstVertex(keptLines[0]).X, 6);
            Assert.Equal(40.0, two.GetFirstVertex(sentLines[0]).X, 6);
            Assert.Equal(left - dogleg, two.GetLastVertex(keptLines[0]).X, 6);
            Assert.Equal(left + 2.0 * gap + ink + dogleg, two.GetLastVertex(sentLines[0]).X, 6);
            Assert.Equal(left + gap, two.TextLocation.X, 6);
            Assert.Equal(DxfAttachmentPoint.TopLeft, two.MText.Attachment);
        }

        [Fact]
        public void ALeaderWhoseArrowheadsAreAllOnTheOtherSideOfTheBox_IsTurnedToIt()
        {
            // AutoCAD 2021: both lines of the leader on the left of the words have their arrowheads past the middle of the box
            // once it is aligned, so the leader goes to the right whole, none left behind.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(40, 0), P(50, 25), "SOD");
            int line = two.AddLeaderLine(two.GetLeaderIndexes()[0]);
            two.AddFirstVertex(line, new GeoPoint3(45, 30, 0));
            double left = one.TextLocation.X - one.LandingGap * one.Scale;

            Run(Session(document), "MLA", new[] { one.Handle, two.Handle }, Enter, new[] { one.Handle }, P(left, 110));

            IReadOnlyList<int> leaders = two.GetLeaderIndexes();
            Assert.Single(leaders);
            Assert.Equal(-1.0, two.GetDogleg(leaders[0]).X, 6);
            Assert.Equal(2, two.GetLeaderLineIndexes(leaders[0]).Count);
        }

        [Fact]
        public void MakeSegmentsParallel_OfSeveralLines_TurnsTheOneLyingMostNearlyAlongTheDogleg_NotTheFirst()
        {
            // AutoCAD 2021: a leader of two lines to one landing, the first from an arrowhead at (90, 70) to the landing at
            // (100, 40) (71.6 degrees off the dogleg's axis) and the second from (70, 34) (11.3 degrees off it): the second is
            // the one turned to run as the leader of the one picked, which leaves at 153.4 degrees; the first only follows
            // the landing.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(-20, 10), "IOI");
            DxfMLeader two = Make(document, P(90, 70), P(100, 40), "SOD");
            Assert.True(two.GetDogleg(two.GetLeaderIndexes()[0]).X > 0.0);
            int line = two.AddLeaderLine(two.GetLeaderIndexes()[0]);
            two.AddFirstVertex(line, new GeoPoint3(70, 34, 0));
            int first = two.GetLeaderLineIndexes(two.GetLeaderIndexes()[0])[0];
            GeoPoint3 firstArrow = two.GetFirstVertex(first);
            GeoPoint3 secondArrow = two.GetFirstVertex(line);
            double length = Distance(secondArrow, two.GetLastVertex(line));
            GeoPoint3 end = one.GetLastVertex(one.GetLeaderLineIndexes(one.GetLeaderIndexes()[0])[0]);
            GeoPoint3 start = one.GetFirstVertex(one.GetLeaderLineIndexes(one.GetLeaderIndexes()[0])[0]);
            double way = Math.Atan2(end.Y - start.Y, end.X - start.X);

            Run(Session(document), "MLA", new[] { one.Handle, two.Handle }, Enter, "O", "P", new[] { one.Handle });

            GeoPoint3 landing = two.GetLastVertex(line);
            Assert.Equal(secondArrow.X + length * Math.Cos(way), landing.X, 5);
            Assert.Equal(secondArrow.Y + length * Math.Sin(way), landing.Y, 5);
            Near(firstArrow, two.GetFirstVertex(first));
            Near(landing, two.GetLastVertex(first));
        }

        private static double Distance(GeoPoint3 a, GeoPoint3 b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
