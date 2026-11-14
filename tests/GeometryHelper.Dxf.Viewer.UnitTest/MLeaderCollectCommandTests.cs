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
    /// MLEADERCOLLECT (MLC), as AutoCAD 2021's console runs it (probed through standard input, multileaders made by MLEADER
    /// in a style of a block): the multileaders stay as they are, each one's block moved so that the blocks stand side by
    /// side (Horizontal), one under another (Vertical) or in rows (Wrap), their boxes touching and their tops level, from
    /// the point given as the top left of the group; one leader is kept, which is the leader lines of the last multileader
    /// selected, landed at the left of the first block, or at the right of the last block of the first row, as the arrowhead is
    /// nearer one landing or the other; the other leaders are taken away. Multileaders of words are filtered out. The blocks
    /// here: TAG a circle of radius 5 round its origin, BOX a rectangle 20 by 10 from its origin up and right, SQ a square 8 by
    /// 8 round its origin, in a style of a dogleg of 8.
    /// </summary>
    public class MLeaderCollectCommandTests
    {
        private sealed class Rig
        {
            public CadDocument Document;
            public DxfMLeaderStyle Style;
            public DxfBlockTableRecord Tag;
            public DxfBlockTableRecord Box;
            public DxfBlockTableRecord Square;
        }

        private static Rig Blocks()
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfDatabase database = document.Database;
            var rig = new Rig { Document = document };
            rig.Tag = new DxfBlockTableRecord { Name = "TAG" };
            rig.Box = new DxfBlockTableRecord { Name = "BOX" };
            rig.Square = new DxfBlockTableRecord { Name = "SQ" };
            database.BlockTable.Add(rig.Tag);
            database.BlockTable.Add(rig.Box);
            database.BlockTable.Add(rig.Square);
            rig.Tag.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 5));
            Rectangle(rig.Box, 0, 0, 20, 10);
            Rectangle(rig.Square, -4, -4, 4, 4);
            rig.Style = CadMLeaderStyleEditor.Find(database, "Standard");
            rig.Style.ContentType = DxfContentType.BlockContent;
            rig.Style.DoglegLength = 8.0;
            return rig;
        }

        private static void Rectangle(DxfBlockTableRecord block, double x0, double y0, double x1, double y1)
        {
            block.AppendEntity(new DxfLine(new GeoPoint3(x0, y0, 0), new GeoPoint3(x1, y0, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(x1, y0, 0), new GeoPoint3(x1, y1, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x0, y1, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(x0, y1, 0), new GeoPoint3(x0, y0, 0)));
        }

        /// <summary>A multileader of a block, made as MLEADER makes one: the arrowhead, the landing and the block as the style has it.</summary>
        private static DxfMLeader Make(Rig rig, DxfBlockTableRecord block, GeoPoint2 arrow, GeoPoint2 landing)
        {
            rig.Style.Block = block;
            int before = All<DxfMLeader>(rig.Document.ModelSpace).Count;
            Run(Session(rig.Document), "MLD", arrow, landing);
            List<DxfMLeader> all = All<DxfMLeader>(rig.Document.ModelSpace);
            Assert.Equal(before + 1, all.Count);
            return all[all.Count - 1];
        }

        private static DxfMLeader Re(Rig rig, DxfMLeader made) => Entity<DxfMLeader>(rig.Document, made.Handle);

        private static string[] Handles(params DxfMLeader[] leaders) => leaders.Select(l => l.Handle).ToArray();

        private static GeoPoint3 Landing(DxfMLeader mleader, int leader = 0) =>
            mleader.GetLastVertex(mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes()[leader])[0]);

        private static GeoPoint3 Arrow(DxfMLeader mleader, int leader = 0, int line = 0) =>
            mleader.GetFirstVertex(mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes()[leader])[line]);

        /// <summary>Three TAG multileaders, arrowheads at (0, 0), (10, 30) and (50, 10), landings (30, 20), (40, 50) and (80, 40).</summary>
        private static Rig Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c)
        {
            Rig rig = Blocks();
            a = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            b = Make(rig, rig.Tag, P(10, 30), P(40, 50));
            c = Make(rig, rig.Tag, P(50, 10), P(80, 40));
            return rig;
        }

        [Fact]
        public void TheCommandAsksInAutoCadsWords()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);
            CadCommandSession session = Run(Session(rig.Document), "MLEADERCOLLECT");
            Assert.Equal("Select multileaders:", session.PromptText);

            Run(session, Handles(a, b, c), Enter);

            Assert.Equal(
                "Specify collected multileader location or [Vertical/Horizontal/Wrap] <Horizontal>:",
                session.PromptText
            );
        }

        [Fact]
        public void ThreeBlocksCollectedHorizontally_StandSideBySide_TheirTopsLevel_AndOneLeaderIsKept()
        {
            // AutoCAD 2021: three TAG multileaders collected at (150, 150) stand at 155, 165 and 175 with their tops at 150,
            // the first one's leader landed 8 left of its block at (142, 145), its arrowhead where the last one's was.
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);

            Run(Session(rig.Document), "MLC", Handles(a, b, c), Enter, P(150, 150));

            a = Re(rig, a);
            b = Re(rig, b);
            c = Re(rig, c);
            Near(new GeoPoint3(155, 145, 0), a.BlockPosition);
            Near(new GeoPoint3(165, 145, 0), b.BlockPosition);
            Near(new GeoPoint3(175, 145, 0), c.BlockPosition);
            Assert.Equal(1, a.LeaderCount);
            Assert.Equal(1, a.LeaderLineCount);
            Assert.Equal(0, b.LeaderCount);
            Assert.Equal(0, c.LeaderCount);
            GeoPoint3 landing = Landing(a);
            Assert.Equal(142.0, landing.X, 6);
            Assert.Equal(145.0, landing.Y, 6);
            GeoPoint3 arrow = Arrow(a);
            Assert.Equal(50.0, arrow.X, 6);
            Assert.Equal(10.0, arrow.Y, 6);
            Assert.Equal(new GeoVector3(1, 0, 0), a.GetDogleg(a.GetLeaderIndexes()[0]));
            Assert.Equal("MLEADERCOLLECT", rig.Document.NextUndo.Name);
            AssertReopens(rig.Document);
        }

        [Fact]
        public void TheSelectionOrderIsTheOrderAcross_TheFirstOneLeftmost_TheLastOnesArrowheadKept()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);

            Run(Session(rig.Document), "MLC", Handles(c, a, b), Enter, P(150, 150));

            c = Re(rig, c);
            a = Re(rig, a);
            b = Re(rig, b);
            Assert.Equal(155.0, c.BlockPosition.X, 6);
            Assert.Equal(165.0, a.BlockPosition.X, 6);
            Assert.Equal(175.0, b.BlockPosition.X, 6);
            Assert.Equal(1, c.LeaderCount);
            Assert.Equal(0, a.LeaderCount);
            GeoPoint3 arrow = Arrow(c);
            Assert.Equal(10.0, arrow.X, 6);
            Assert.Equal(30.0, arrow.Y, 6);
        }

        [Fact]
        public void BlocksOfDifferentSizesStandByTheirExtents_WithTheirTopsLevel()
        {
            // AutoCAD 2021: BOX 20 by 10, SQ 8 by 8 and TAG 10 by 10 from (150, 150): the box's origin at (150, 140), the
            // square's middle at (174, 146) and the tag's at (183, 145), the leader landed 8 left of the box at (142, 145).
            Rig rig = Blocks();
            DxfMLeader box = Make(rig, rig.Box, P(0, 0), P(30, 20));
            DxfMLeader square = Make(rig, rig.Square, P(10, 30), P(40, 50));
            DxfMLeader tag = Make(rig, rig.Tag, P(50, 10), P(80, 40));

            Run(Session(rig.Document), "MLC", Handles(box, square, tag), Enter, P(150, 150));

            Near(new GeoPoint3(150, 140, 0), Re(rig, box).BlockPosition);
            Near(new GeoPoint3(174, 146, 0), Re(rig, square).BlockPosition);
            Near(new GeoPoint3(183, 145, 0), Re(rig, tag).BlockPosition);
            Assert.Equal(142.0, Landing(Re(rig, box)).X, 6);
            Assert.Equal(145.0, Landing(Re(rig, box)).Y, 6);
        }

        [Fact]
        public void Vertical_StacksTheBlocksDown_AlignedAtTheLeft_TheLeaderOnTheLastOne()
        {
            // AutoCAD 2021: Vertical stacks BOX, SQ and TAG from (150, 150): their lefts at 150, the box's origin at (150, 140),
            // the square's middle at (154, 136), the tag's at (155, 127); the leader is the last block's, landed at (142, 127),
            // its arrowhead its own.
            Rig rig = Blocks();
            DxfMLeader box = Make(rig, rig.Box, P(0, 0), P(30, 20));
            DxfMLeader square = Make(rig, rig.Square, P(10, 30), P(40, 50));
            DxfMLeader tag = Make(rig, rig.Tag, P(50, 10), P(80, 40));

            Run(Session(rig.Document), "MLC", Handles(box, square, tag), Enter, "V", P(150, 150));

            Near(new GeoPoint3(150, 140, 0), Re(rig, box).BlockPosition);
            Near(new GeoPoint3(154, 136, 0), Re(rig, square).BlockPosition);
            Near(new GeoPoint3(155, 127, 0), Re(rig, tag).BlockPosition);
            Assert.Equal(0, Re(rig, box).LeaderCount);
            Assert.Equal(0, Re(rig, square).LeaderCount);
            GeoPoint3 landing = Landing(Re(rig, tag));
            Assert.Equal(142.0, landing.X, 6);
            Assert.Equal(127.0, landing.Y, 6);
            Assert.Equal(50.0, Arrow(Re(rig, tag)).X, 6);
        }

        [Fact]
        public void Wrap_BreaksTheRowWhereTheNextBlockWouldReachTheWidth_TheRowsFollowingEachOtherDown()
        {
            // AutoCAD 2021: four TAGs under a wrap width of 25 stand two to a row, the second row 10 down: a width of exactly
            // 20 breaks two blocks of 10 apart, 20.0001 keeps them in a row; 30.0001 holds three.
            Rig rig = Blocks();
            var tags = new List<DxfMLeader>();

            for (int i = 0; i < 4; i++)
            {
                tags.Add(Make(rig, rig.Tag, P(10 * i, 0), P(10 * i + 30, 20)));
            }

            Run(Session(rig.Document), "MLC", Handles(tags.ToArray()), Enter, "W", "25", P(150, 150));

            double[] xs = tags.Select(t => Re(rig, t).BlockPosition.X).ToArray();
            double[] ys = tags.Select(t => Re(rig, t).BlockPosition.Y).ToArray();
            Assert.Equal(new[] { 155.0, 165.0, 155.0, 165.0 }, xs);
            Assert.Equal(new[] { 145.0, 145.0, 135.0, 135.0 }, ys);
            Assert.Equal(1, Re(rig, tags[0]).LeaderCount);
            Assert.Equal(0, Re(rig, tags[1]).LeaderCount);
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(tags.ToArray()), Enter, "W", "20", P(150, 150));
            Assert.Equal(new[] { 155.0, 155.0, 155.0, 155.0 }, tags.Select(t => Re(rig, t).BlockPosition.X).ToArray());
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(tags.ToArray()), Enter, "W", "20.0001", P(150, 150));
            Assert.Equal(new[] { 155.0, 165.0, 155.0, 165.0 }, tags.Select(t => Re(rig, t).BlockPosition.X).ToArray());
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(tags.ToArray()), Enter, "W", "30.0001", P(150, 150));
            Assert.Equal(new[] { 155.0, 165.0, 175.0, 155.0 }, tags.Select(t => Re(rig, t).BlockPosition.X).ToArray());
        }

        [Fact]
        public void Wrap_ByNumberPerRow_PutsThatManyInARow_AndTheNextRowIsAsHighAsTheTallestOfTheRowAbove()
        {
            // AutoCAD 2021: BOX, SQ, TAG, BOX by two to a row: the second row's tops 10 below the first's (the box 10 high).
            Rig rig = Blocks();
            DxfMLeader box = Make(rig, rig.Box, P(0, 0), P(30, 20));
            DxfMLeader square = Make(rig, rig.Square, P(10, 30), P(40, 50));
            DxfMLeader tag = Make(rig, rig.Tag, P(50, 10), P(80, 40));
            DxfMLeader box2 = Make(rig, rig.Box, P(5, 5), P(35, 30));

            Run(Session(rig.Document), "MLC", Handles(box, square, tag, box2), Enter, "W", "N", "2", P(150, 150));

            Near(new GeoPoint3(150, 140, 0), Re(rig, box).BlockPosition);
            Near(new GeoPoint3(174, 146, 0), Re(rig, square).BlockPosition);
            Near(new GeoPoint3(155, 135, 0), Re(rig, tag).BlockPosition);
            Near(new GeoPoint3(160, 130, 0), Re(rig, box2).BlockPosition);
        }

        [Fact]
        public void TheOptionsAreAskedAndRemembered_AsAutoCadAsksThem()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);
            CadCommandSession session = Run(Session(rig.Document), "MLC", Handles(a, b, c), Enter, "W");
            Assert.Equal("Specify wrap width or [Number]: <0.000000>", session.PromptText);

            Run(session, "N");
            Assert.Equal("Specify number per row <1>:", session.PromptText);

            Run(session, "0");
            Assert.Equal("Value must be positive and nonzero.", session.History.Last());
            Assert.Equal("Specify number per row <1>:", session.PromptText);

            Run(session, "3");
            Assert.Equal(
                "Specify collected multileader location or [Vertical/Horizontal/Wrap] <Wrap>:",
                session.PromptText
            );

            Run(session, "V");
            Assert.Equal(
                "Specify collected multileader location or [Vertical/Horizontal/Wrap] <Vertical>:",
                session.PromptText
            );

            Run(session, "W");
            Assert.Equal("Specify wrap width or [Number]: <0.000000>", session.PromptText);

            Run(session, "25");
            Run(session, P(150, 150));
            Assert.False(session.IsRunning);

            CadCommandSession next = Run(Session(rig.Document), "MLC", Handles(a, b, c), Enter);
            Assert.Equal(
                "Specify collected multileader location or [Vertical/Horizontal/Wrap] <Horizontal>:",
                next.PromptText
            );
        }

        [Fact]
        public void TheLeaderIsLandedOnTheRight_WhereTheLastArrowheadIsPastTheMiddleBetweenTheTwoLandings()
        {
            // AutoCAD 2021: three TAGs in a row from (150, 150), landings at 142 and 188 (the row ends at 180): an arrowhead at
            // x = 163 is kept on the left, one at 167 on the right, the last block's own, its dogleg against x.
            Rig rig = Blocks();
            DxfMLeader a = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            DxfMLeader b = Make(rig, rig.Tag, P(10, 30), P(40, 50));
            DxfMLeader left = Make(rig, rig.Tag, P(163, -40), P(193, -20));
            DxfMLeader right = Make(rig, rig.Tag, P(167, -40), P(197, -20));

            Run(Session(rig.Document), "MLC", Handles(a, b, left), Enter, P(150, 150));
            Assert.Equal(1, Re(rig, a).LeaderCount);
            Assert.Equal(0, Re(rig, left).LeaderCount);
            Assert.Equal(142.0, Landing(Re(rig, a)).X, 6);
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(a, b, right), Enter, P(150, 150));
            DxfMLeader last = Re(rig, right);
            Assert.Equal(0, Re(rig, a).LeaderCount);
            Assert.Equal(1, last.LeaderCount);
            Assert.Equal(188.0, Landing(last).X, 6);
            Assert.Equal(145.0, Landing(last).Y, 6);
            Assert.Equal(new GeoVector3(-1, 0, 0), last.GetDogleg(last.GetLeaderIndexes()[0]));
            Assert.Equal(167.0, Arrow(last).X, 6);
        }

        [Fact]
        public void InAColumn_TheLeaderGoesToTheLastBlock_ItsSideThatOfTheMiddleOfThatBlock()
        {
            // AutoCAD 2021: BOX then TAG then TAG in a column: the middle of the last block is at 155, an arrowhead at 154
            // is kept on the left and one at 156 on the right, landed 8 past the block's right edge at 168.
            Rig rig = Blocks();
            DxfMLeader box = Make(rig, rig.Box, P(0, 0), P(30, 20));
            DxfMLeader tag = Make(rig, rig.Tag, P(10, 30), P(40, 50));
            DxfMLeader near = Make(rig, rig.Tag, P(154, -40), P(184, -20));
            DxfMLeader far = Make(rig, rig.Tag, P(156, -40), P(186, -20));

            Run(Session(rig.Document), "MLC", Handles(box, tag, near), Enter, "V", P(150, 150));
            Assert.Equal(142.0, Landing(Re(rig, near)).X, 6);
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(box, tag, far), Enter, "V", P(150, 150));
            Assert.Equal(168.0, Landing(Re(rig, far)).X, 6);
            Assert.Equal(0, Re(rig, box).LeaderCount);
        }

        [Fact]
        public void TheLeaderLinesOfTheLastMultileaderAreKept_AndALeaderOnEachSideGoesToItsEnd()
        {
            // AutoCAD 2021: the last multileader has a leader on its left (arrowhead (0, 200)) and one on its right ((300, 190)):
            // collected after a TAG, the TAG keeps the left one landed at 142 and the last block the right one landed 8 past
            // the right of the row, at 178 for a TAG and a box 20 wide at 150 and 160 (150 + 10 + 20 + 8 = 188).
            Rig rig = Blocks();
            DxfMLeader tag = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            DxfMLeader two = Make(rig, rig.Box, P(0, 200), P(30, 220));
            Run(Session(rig.Document), "MLE", new[] { two.Handle }, P(300, 190), Enter);
            two = Re(rig, two);
            Assert.Equal(2, two.LeaderCount);

            Run(Session(rig.Document), "MLC", Handles(tag, two), Enter, P(150, 150));

            tag = Re(rig, tag);
            two = Re(rig, two);
            Assert.Equal(1, tag.LeaderCount);
            Assert.Equal(1, two.LeaderCount);
            Assert.Equal(142.0, Landing(tag).X, 6);
            Assert.Equal(0.0, Arrow(tag).X, 6);
            Assert.Equal(200.0, Arrow(tag).Y, 6);
            Assert.Equal(188.0, Landing(two).X, 6);
            Assert.Equal(300.0, Arrow(two).X, 6);
            Assert.Equal(190.0, Arrow(two).Y, 6);
            Assert.Equal(new GeoVector3(-1, 0, 0), two.GetDogleg(two.GetLeaderIndexes()[0]));
        }

        [Fact]
        public void TwoLinesOfOneLeader_StayOneLeader_OnTheBlockThatTheirSideLandsAt()
        {
            // AutoCAD 2021: a last multileader of one leader with two lines (arrowheads (0, 100) and (10, 130)), collected after
            // two TAGs, gives its two lines to the first TAG as one leader landed at 142, and keeps none itself.
            Rig rig = Blocks();
            DxfMLeader a = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            DxfMLeader b = Make(rig, rig.Tag, P(10, 30), P(40, 50));
            DxfMLeader two = Make(rig, rig.Box, P(0, 100), P(30, 120));
            Run(Session(rig.Document), "MLE", new[] { two.Handle }, P(10, 130), Enter);
            Assert.Equal(2, Re(rig, two).LeaderLineCount);

            Run(Session(rig.Document), "MLC", Handles(a, b, two), Enter, P(150, 150));

            a = Re(rig, a);
            Assert.Equal(1, a.LeaderCount);
            Assert.Equal(2, a.LeaderLineCount);
            IReadOnlyList<int> lines = a.GetLeaderLineIndexes(a.GetLeaderIndexes()[0]);
            Assert.Equal(0.0, a.GetFirstVertex(lines[0]).X, 6);
            Assert.Equal(100.0, a.GetFirstVertex(lines[0]).Y, 6);
            Assert.Equal(10.0, a.GetFirstVertex(lines[1]).X, 6);
            Assert.Equal(130.0, a.GetFirstVertex(lines[1]).Y, 6);
            Assert.Equal(142.0, Landing(a).X, 6);
            Assert.Equal(0, Re(rig, b).LeaderCount);
            Assert.Equal(0, Re(rig, two).LeaderCount);
        }

        [Fact]
        public void ALoneMultileader_IsMovedToThePoint_ItsLeaderKeptAtItsOwnArrowhead()
        {
            // AutoCAD 2021: one TAG, arrowhead (10, 30), collected at (150, 150): its top left at the point and its leader
            // landed at (142, 145).
            Rig rig = Blocks();
            DxfMLeader tag = Make(rig, rig.Tag, P(0, 0), P(30, 20));

            Run(Session(rig.Document), "MLC", Handles(tag), Enter, P(150, 150));

            Near(new GeoPoint3(155, 145, 0), Re(rig, tag).BlockPosition);
            Assert.Equal(142.0, Landing(Re(rig, tag)).X, 6);
            Assert.Equal(0.0, Arrow(Re(rig, tag)).X, 6);
        }

        [Fact]
        public void AMultileaderOfScaleTwo_StandsByItsScaledExtents_AndLandsByItsScaledDogleg()
        {
            // AutoCAD 2021 at MLEADERSCALE 2: the TAG is 20 wide (a radius of 10) and its dogleg 16; collected after a TAG of
            // scale 1 it stands at 170 with its top at 150 (middle y 140); collected first, its landing is at 134.
            Rig rig = Blocks();
            DxfMLeader one = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            CadCommandSession scale = Session(rig.Document);
            Run(scale, "MLEADERSCALE", "2");
            DxfMLeader two = Make(rig, rig.Tag, P(0, 0), P(30, 20));
            Run(Session(rig.Document), "MLEADERSCALE", "1");
            Assert.Equal(2.0, two.Scale, 6);

            Run(Session(rig.Document), "MLC", Handles(one, two), Enter, P(150, 150));
            Assert.Equal(155.0, Re(rig, one).BlockPosition.X, 6);
            Assert.Equal(170.0, Re(rig, two).BlockPosition.X, 6);
            Assert.Equal(140.0, Re(rig, two).BlockPosition.Y, 6);
            Assert.True(rig.Document.Undo());

            Run(Session(rig.Document), "MLC", Handles(two, one), Enter, P(150, 150));
            Assert.Equal(160.0, Re(rig, two).BlockPosition.X, 6);
            Assert.Equal(140.0, Re(rig, two).BlockPosition.Y, 6);
            Assert.Equal(175.0, Re(rig, one).BlockPosition.X, 6);
            Assert.Equal(145.0, Re(rig, one).BlockPosition.Y, 6);
            Assert.Equal(134.0, Landing(Re(rig, two)).X, 6);
        }

        [Fact]
        public void MultileadersOfWordsAndOtherObjects_AreFilteredOut_TheBlocksStillCollected()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out _);
            rig.Style.ContentType = DxfContentType.MTextContent;
            Run(Session(rig.Document), "MLD", P(0, 0), P(30, 20), "ONE");
            DxfMLeader words = All<DxfMLeader>(rig.Document.ModelSpace).Last();
            Assert.Equal(DxfContentType.MTextContent, words.ContentType);
            Run(Session(rig.Document), "LINE", P(300, 300), P(310, 300), Enter);
            string line = All<DxfLine>(rig.Document.ModelSpace).Last().Handle;

            CadCommandSession session = Run(
                Session(rig.Document),
                "MLC",
                new[] { a.Handle, words.Handle, line, b.Handle },
                Enter,
                P(150, 150)
            );

            Assert.Contains("2 were filtered out", session.History);
            Assert.Equal(155.0, Re(rig, a).BlockPosition.X, 6);
            Assert.Equal(165.0, Re(rig, b).BlockPosition.X, 6);
            Assert.Equal(1, Entity<DxfMLeader>(rig.Document, words.Handle).LeaderCount);
            Near(new GeoPoint3(30, 20, 0), Landing(Entity<DxfMLeader>(rig.Document, words.Handle)));
        }

        [Fact]
        public void WithNoBlockMultileaderSelected_ItAsksAgain_AndEnterEndsIt()
        {
            Rig rig = Three(out _, out _, out _);
            rig.Style.ContentType = DxfContentType.MTextContent;
            Run(Session(rig.Document), "MLD", P(0, 0), P(30, 20), "ONE");
            string words = All<DxfMLeader>(rig.Document.ModelSpace).Last().Handle;

            CadCommandSession session = Run(Session(rig.Document), "MLC", new[] { words }, Enter);

            Assert.Contains("1 was filtered out", session.History);
            Assert.Equal("Select multileaders:", session.PromptText);
            Assert.True(session.IsRunning);

            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.False(rig.Document.NextUndo != null && rig.Document.NextUndo.Name == "MLEADERCOLLECT");
        }

        [Fact]
        public void AMultileaderOnALockedLayer_IsLeftWhereItIs_AndSaid()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);
            rig.Document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "LOCKED" });
            rig.Document.Database.LayerTable["LOCKED"].IsLocked = true;
            a.Layer = "LOCKED";
            GeoPoint3 was = a.BlockPosition;

            CadCommandSession session = Run(Session(rig.Document), "MLC", Handles(a, b, c), Enter, P(150, 150));

            Assert.Contains("1 was on a locked layer.", session.History);
            Near(was, Re(rig, a).BlockPosition);
            Assert.Equal(1, Re(rig, a).LeaderCount);
            Assert.Equal(155.0, Re(rig, b).BlockPosition.X, 6);
            Assert.Equal(165.0, Re(rig, c).BlockPosition.X, 6);
            Assert.Equal(1, Re(rig, b).LeaderCount);
            Assert.Equal(0, Re(rig, c).LeaderCount);
        }

        [Fact]
        public void TheWholeCollectionIsOneUndoStep_AndUndoPutsEveryMultileaderBack()
        {
            Rig rig = Three(out DxfMLeader a, out DxfMLeader b, out DxfMLeader c);
            string start = rig.Document.Database.ToText();

            Run(Session(rig.Document), "MLEADERCOLLECT", Handles(a, b, c), Enter, P(150, 150));
            Assert.NotEqual(start, rig.Document.Database.ToText());
            Assert.Equal("MLEADERCOLLECT", rig.Document.NextUndo.Name);

            Assert.True(rig.Document.Undo());
            Assert.Equal(start, rig.Document.Database.ToText());
            Assert.True(rig.Document.Redo());
            Assert.Equal(155.0, Re(rig, a).BlockPosition.X, 6);
        }

        [Fact]
        public void TheCommandIsOfTheRegistryAndTheAliasMlc()
        {
            Assert.Equal("MLEADERCOLLECT", CadShortcuts.Default.Aliases["MLC"]);
            Assert.Contains(CadCommandRegistry.CreateDefault().Commands, command =>
                string.Equals(command.Name, "MLEADERCOLLECT", StringComparison.Ordinal)
            );
        }
    }
}
