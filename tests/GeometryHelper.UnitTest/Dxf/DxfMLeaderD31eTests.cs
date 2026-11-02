using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The leftovers of D31d on a MULTILEADER, as AutoCAD 2021 answered them (plugin D31e, rounds E and G, on a Standard of words
    /// 4 high, arrows 4, a gap of 2 and doglegs of 8): a leader of no landing taking the first vertex given it as its landing, a
    /// dogleg turned landing its leader, the block's scale and turn set to what they are, the block moved and its matrix, a mirror
    /// across the XY plane, and words given before the multileader is added.
    /// </summary>
    public class DxfMLeaderD31eTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        private static DxfDatabase Drawing() => DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1E-6, "expected " + expected.ToString("R", CultureInfo.InvariantCulture) + ", got " + actual.ToString("R", CultureInfo.InvariantCulture));

        private static void NearAll(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], actual[i]);
            }
        }

        /// <summary>The box of lines from (-10, -5) to (20, 15), GHD31E's BOX.</summary>
        private static DxfBlockTableRecord Box(DxfDatabase db)
        {
            var block = new DxfBlockTableRecord { Name = "GH_BOX" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
            block.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));
            block.AppendEntity(new DxfLine(P(20, 15), P(-10, 15)));
            block.AppendEntity(new DxfLine(P(-10, 15), P(-10, -5)));
            return block;
        }

        /// <summary>A multileader of the box placed at (100, 50), as GHD31E made AutoCAD's, then its lines from the points given.</summary>
        private static DxfMLeader OfBox(DxfDatabase db, GeoPoint3[] points, Action<DxfMLeader> placed = null)
        {
            DxfBlockTableRecord block = Box(db);
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.BlockContent = block;
            mleader.BlockPosition = P(100, 50);
            placed?.Invoke(mleader);

            foreach (GeoPoint3 point in points)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        /// <summary>A multileader of "ABC" 4 high at (100, 50), as GHD31E made AutoCAD's, then its lines from the points given.</summary>
        private static DxfMLeader OfWords(DxfDatabase db, GeoPoint3[] points, Action<DxfMLeader> before = null)
        {
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            before?.Invoke(mleader);
            mleader.MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0 };

            foreach (GeoPoint3 point in points)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        private static GeoPoint3[] Points(string side) =>
            side == "left" ? new[] { P(0, 0) } : side == "right" ? new[] { P(250, 0) } : side == "both" ? new[] { P(0, 0), P(250, 0) } : new GeoPoint3[0];

        /// <summary>A leader of no lines added, and a line of no vertex in it; the line's index.</summary>
        private static int Empty(DxfMLeader mleader, GeoVector3? dogleg = null)
        {
            int leader = mleader.AddLeader();

            if (dogleg.HasValue)
            {
                mleader.SetDogleg(leader, dogleg.Value);
            }

            return mleader.AddLeaderLine(leader);
        }

        /// <summary>GHD31D's second leader on the left: of its own, from (-20, 80) landed at (40, 70).</summary>
        private static void SecondLeft(DxfMLeader mleader)
        {
            int line = Empty(mleader);
            mleader.AddFirstVertex(line, P(-20, 80));
            mleader.SetLastVertex(line, P(40, 70));
        }

        /// <summary>GHD31D's second leader on the right: its dogleg against x, from (270, 80) landed at (200, 70).</summary>
        private static void SecondRight(DxfMLeader mleader)
        {
            int line = Empty(mleader, new GeoVector3(-1, 0, 0));
            mleader.AddFirstVertex(line, P(270, 80));
            mleader.SetLastVertex(line, P(200, 70));
        }

        private static List<DxfTypedValue> Tail(DxfEntity entity) => entity.Item.File.TailOf(entity.Item, 0);

        /// <summary>The text of the first group of a code in the multileader's record after the first group of a value (as <c>LEADER{</c>) or code.</summary>
        private static string After(DxfEntity entity, string from, int code, int skip = 0)
        {
            List<DxfTypedValue> tail = Tail(entity);
            int start = tail.FindIndex(g => int.TryParse(from, out int at) ? g.TypeCode == at : g.Value.Trim() == from);
            return tail.Skip(start + 1).Where(g => g.TypeCode == code).Skip(skip).Select(g => g.Value.Trim()).FirstOrDefault();
        }

        /// <summary>The text of the first group of a code of a leader of the context data (its LEADER{ section, counted from nought).</summary>
        private static string OfLeader(DxfEntity entity, int leader, int code)
        {
            List<DxfTypedValue> tail = Tail(entity);
            int start = Enumerable.Range(0, tail.Count).Where(i => tail[i].Value.Trim() == "LEADER{").Skip(leader).First();
            return tail.Skip(start + 1).TakeWhile(g => g.TypeCode != 303).Where(g => g.TypeCode == code).Select(g => g.Value.Trim()).FirstOrDefault();
        }

        /// <summary>The point a leader's dogleg starts at, as written (its 10, 20 and 30).</summary>
        private static GeoPoint3 LeaderPoint(DxfEntity entity, int leader) =>
            new GeoPoint3(Number(OfLeader(entity, leader, 10)), Number(OfLeader(entity, leader, 20)), Number(OfLeader(entity, leader, 30)));

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static int Flags(DxfMLeader mleader) => int.Parse(After(mleader, "301", 90), CultureInfo.InvariantCulture);

        /// <summary>The block's matrix (its 47 groups), row by row, as written.</summary>
        private static double[] Matrix(DxfMLeader mleader, int rows = 2) => Enumerable.Range(0, 4 * rows).Select(i => Number(After(mleader, "296", 47, i))).ToArray();

        private static GeoPoint3 Point(DxfMLeader mleader, string from, int code, int skip = 0) =>
            new GeoPoint3(Number(After(mleader, from, code, skip)), Number(After(mleader, from, code + 10, skip)), Number(After(mleader, from, code + 20, skip)));

        private static List<DxfLine> Doglegs(DxfMLeader mleader) => mleader.Explode().OfType<DxfLine>().ToList();

        // ---- a leader of no landing (item 1) -----------------------------------------------------------------------------------

        [Fact]
        public void ALeaderAdded_HasNoLanding_ItsLineNoVertex_AndNoDoglegIsDrawn_AsAutoCadWritesIt()
        {
            // AutoCAD 2021: AddLeader wrote a leader of 290 0, its line of AddLeaderLine had no vertex, and the leader drew no
            // dogleg (a1-addleader, a1-addleader-line). The library landed it at the base point, its line of one vertex there.
            DxfMLeader mleader = OfWords(Drawing(), Points("left"));

            int line = Empty(mleader);

            Assert.Equal("0", OfLeader(mleader, 1, 290));
            Assert.Equal(0, mleader.VerticesCount(line));
            Assert.Single(Doglegs(mleader));

            // AutoCAD 2021: the words of no other leader stayed under a dogleg of 15 (none-addleader-dl15).
            DxfMLeader alone = OfWords(Drawing(), Points("none"));
            alone.AddLeader();
            alone.DoglegLength = 15;
            Near(P(100, 50), alone.TextLocation);
        }

        [Theory]
        [InlineData("words")]
        [InlineData("block")]
        public void AddFirstVertex_OnALeaderOfNoLanding_TakesThePointAsItsLanding_AsAutoCadDoes(string content)
        {
            // AutoCAD 2021: AddFirstVertex at (-20, 80) on the line of a leader just added made it the leader's landing, the line
            // of one vertex, the dogleg drawn from (-20, 80) to (-12, 80), the content left (a1-addfirst, a1-k-addfirst). The
            // library put the point before a landing at the base point.
            DxfMLeader mleader = content == "words" ? OfWords(Drawing(), Points("left")) : OfBox(Drawing(), Points("left"));
            int line = Empty(mleader);

            mleader.AddFirstVertex(line, P(-20, 80));

            Assert.Equal(1, mleader.VerticesCount(line));
            Near(P(-20, 80), mleader.GetLastVertex(line));
            Assert.Equal("1", OfLeader(mleader, 1, 290));
            Assert.Contains(Doglegs(mleader), d => d.StartPoint.IsEqualTo(P(-20, 80), Close) && d.EndPoint.IsEqualTo(P(-12, 80), Close));
            Near(P(100, 50), content == "words" ? mleader.TextLocation : mleader.BlockPosition);
        }

        [Fact]
        public void TheVerticesAfterTheFirst_AndAnotherLine_GoBeforeTheLanding_AsAutoCadPutsThem()
        {
            // AutoCAD 2021: a second AddFirstVertex at (-30, 90) put it before the landing (a1-addfirst-twice); AddLastVertex on a
            // line of no vertex landed the leader there too (a1-addlast); a second line of the leader landed from (-30, 90) to
            // (-20, 80) (a1-addfirst-line2); with no dogleg the landing's start was written at (-28, 80) (a1-nodogleg-addfirst).
            DxfMLeader twice = OfWords(Drawing(), Points("left"));
            int line = Empty(twice);
            twice.AddFirstVertex(line, P(-20, 80));
            twice.AddFirstVertex(line, P(-30, 90));
            Assert.Equal(2, twice.VerticesCount(line));
            Near(P(-30, 90), twice.GetFirstVertex(line));
            Near(P(-20, 80), twice.GetLastVertex(line));

            DxfMLeader last = OfWords(Drawing(), Points("left"));
            int lastLine = Empty(last);
            last.AddLastVertex(lastLine, P(-20, 80));
            Assert.Equal(1, last.VerticesCount(lastLine));
            Near(P(-20, 80), last.GetLastVertex(lastLine));

            DxfMLeader second = OfWords(Drawing(), Points("left"));
            int first = Empty(second);
            second.AddFirstVertex(first, P(-20, 80));
            int other = second.AddLeaderLine(second.GetLeaderIndex(first));
            second.AddFirstVertex(other, P(-30, 90));
            Assert.Equal(2, second.VerticesCount(other));
            Near(P(-20, 80), second.GetLastVertex(other));

            DxfMLeader straight = OfWords(Drawing(), Points("left"), m => m.EnableDogleg = false);
            int straightLine = Empty(straight);
            straight.AddFirstVertex(straightLine, P(-20, 80));
            Assert.Equal(1, straight.VerticesCount(straightLine));
            Near(P(-20, 80), straight.GetLastVertex(straightLine));
            Near(P(-28, 80), LeaderPoint(straight, 1));
        }

        [Theory]
        [InlineData("words", 1.0, 90.0, 48.0)]
        [InlineData("block", 1.0, 82.0, 55.0)]
        [InlineData("block", -1.0, 128.0, 55.0)]
        public void SetDogleg_OnALeaderOfNoLanding_LandsItAtTheContent_AndAVertexGoesBeforeIt_AsAutoCadDoes(string content, double along, double x, double y)
        {
            // AutoCAD 2021: SetDogleg on a leader just added landed it on the side of the content it runs from, its first vertex
            // then put before the landing: along x on the left of the words from (90, 48) (a1-setdogleg-addfirst), against x on
            // the right of the box from (128, 55) (a1-k-negdogleg-addfirst).
            DxfMLeader mleader = content == "words" ? OfWords(Drawing(), Points("left")) : OfBox(Drawing(), Points("left"));
            int line = Empty(mleader, new GeoVector3(along, 0, 0));

            mleader.AddFirstVertex(line, P(along > 0 ? -20 : 270, 80));

            Assert.Equal(2, mleader.VerticesCount(line));
            Near(P(x, y), mleader.GetLastVertex(line));
            Assert.Equal("1", OfLeader(mleader, 1, 290));
        }

        [Fact]
        public void SetDogleg_TurningALandedLeader_LandsItOnTheOtherSide_AsAutoCadDoes()
        {
            // AutoCAD 2021: the box's leader from the left, its dogleg turned against x, was landed on the box's right from (128,
            // 55) (k-left-setdogleg-neg); the dogleg set as it was moved nothing (words-left-setdogleg-same).
            DxfMLeader mleader = OfBox(Drawing(), Points("left"));

            mleader.SetDogleg(0, new GeoVector3(-1, 0, 0));

            Near(P(128, 55), mleader.GetLastVertex(0));
            Near(P(100, 50), mleader.BlockPosition);

            DxfMLeader words = OfWords(Drawing(), Points("left"));
            words.SetLastVertex(0, P(40, 70));
            words.SetDogleg(0, new GeoVector3(1, 0, 0));
            Near(P(40, 70), words.GetLastVertex(0));
            Near(P(50, 72), words.TextLocation);
        }

        [Fact]
        public void ALandedLeaderOfNoLine_DrawsItsDogleg_AsAutoCadDrawsIt()
        {
            // AutoCAD 2021: a leader of no line landed on the box's right by SetDogleg drew its dogleg from (128, 55) to (120, 55)
            // (a1-k-right-negdogleg-only), as the leader of a line beside it.
            DxfMLeader mleader = OfBox(Drawing(), Points("right"));

            mleader.SetDogleg(mleader.AddLeader(), new GeoVector3(-1, 0, 0));

            List<DxfLine> doglegs = Doglegs(mleader);
            Assert.Equal(2, doglegs.Count);
            Assert.All(doglegs, d => Assert.True(d.StartPoint.IsEqualTo(P(128, 55), Close) && d.EndPoint.IsEqualTo(P(120, 55), Close)));
        }

        [Theory]
        [InlineData("words")]
        [InlineData("block")]
        public void TwoLeadersMadeByTheLibrary_StandAsAutoCadsOwn(string content)
        {
            // AutoCAD 2021: a second leader added, its line given (-20, 80) and its last vertex set to (40, 70), moved the content by
            // (60, -10): the words to (160, 40), the first landed from (150, 38), the second's line of one vertex (40, 70)
            // (s2-two-base); the box to (160, 40), the first from (142, 45) (k-two-unlanded-base). The library moved them by
            // (-58, 22), to (42, 72).
            DxfMLeader mleader = content == "words" ? OfWords(Drawing(), Points("left")) : OfBox(Drawing(), Points("left"));

            SecondLeft(mleader);

            Near(P(160, 40), content == "words" ? mleader.TextLocation : mleader.BlockPosition);
            Near(content == "words" ? P(150, 38) : P(142, 45), mleader.GetLastVertex(0));
            Assert.Equal(1, mleader.VerticesCount(1));
            Near(P(40, 70), mleader.GetLastVertex(1));
        }

        [Theory]
        [InlineData("SetDoglegLength0", 143.0, 150.0, 160.0, 40.0)]
        [InlineData("SetDoglegLength1", 150.0, 143.0, 160.0, 40.0)]
        [InlineData("DoglegLength", 143.0, 143.0, 160.0, 40.0)]
        [InlineData("LandingGap", 150.0, 150.0, 163.0, 40.0)]
        [InlineData("TextHeight", 150.0, 150.0, 160.0, 43.0)]
        [InlineData("Scale", 142.0, 142.0, 162.0, 42.0)]
        public void TwoLeadersMadeByTheLibrary_AreEditedAsAutoCadsOwn(string change, double first, double second, double x, double y)
        {
            // AutoCAD 2021, on the two leaders it made so (s2-two-base): a dogleg of 15 given to the first started it at (143, 38)
            // and landed the second from (150, 38) (d6-two-setdl0), given to the second (d6-two-setdl1), to both (d-two-dl15); a gap
            // of 5 moved the words to (163, 40) (d-two-gap5), a height of 10 to (160, 43) (two-unlanded-height10), a scale of 2 to
            // (162, 42), both from (142, 38) (d-two-scale2).
            DxfMLeader mleader = OfWords(Drawing(), Points("left"));
            SecondLeft(mleader);

            switch (change)
            {
                case "SetDoglegLength0": mleader.SetDoglegLength(0, 15); break;
                case "SetDoglegLength1": mleader.SetDoglegLength(1, 15); break;
                case "DoglegLength": mleader.DoglegLength = 15; break;
                case "LandingGap": mleader.LandingGap = 5; break;
                case "TextHeight": mleader.TextHeight = 10; break;
                default: mleader.Scale = 2; break;
            }

            Near(P(first, 38), mleader.GetLastVertex(0));
            Near(P(second, 38), mleader.GetLastVertex(1));
            Near(P(x, y), mleader.TextLocation);
        }

        [Fact]
        public void TwoLeadersOfABlockMadeByTheLibrary_AreEditedAsAutoCadsOwn()
        {
            // AutoCAD 2021, on the box's two leaders made so: a dogleg of 15 started both at (135, 45), the box staying at (160, 40)
            // (d-k-two-dl15); BlockScale 2 moved the box to (170, 35), both from (142, 45) (k-two-unlanded-blockscale2).
            DxfMLeader lengthened = OfBox(Drawing(), Points("left"));
            SecondLeft(lengthened);
            lengthened.DoglegLength = 15;
            Near(P(160, 40), lengthened.BlockPosition);
            Near(P(135, 45), lengthened.GetLastVertex(0));
            Near(P(135, 45), lengthened.GetLastVertex(1));

            DxfMLeader scaled = OfBox(Drawing(), Points("left"));
            SecondLeft(scaled);
            scaled.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(170, 35), scaled.BlockPosition);
            Near(P(142, 45), scaled.GetLastVertex(0));
            Near(P(142, 45), scaled.GetLastVertex(1));
        }

        // ---- the last vertex of a leader on the right of a block (item 2) ------------------------------------------------------

        [Theory]
        [InlineData("right", 154.0)]
        [InlineData("none", 0.0)]
        [InlineData("left", 154.0)]
        public void ASecondLeaderOnTheRightOfABlock_MovesTheBlockByItsLanding_AsAutoCadDoes(string side, double leftX)
        {
            // AutoCAD 2021: a leader added on the box's right by SetDogleg against x, landed at (128, 55), its line from (270, 80)
            // and its last vertex set to (200, 70), moved the box by (72, 15) to (172, 65), a leader on the right landed with it
            // at (200, 70), one on the left at (154, 70) (k-right2-base, a1-k-negdogleg-setlast, b2-k-left-negdogleg-setlast). The
            // library moved the box by the move from the base point, to (210, 65).
            DxfMLeader mleader = OfBox(Drawing(), Points(side));

            SecondRight(mleader);

            Near(P(172, 65), mleader.BlockPosition);
            int last = mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes().Last()).Single();
            Near(P(270, 80), mleader.GetFirstVertex(last));
            Near(P(200, 70), mleader.GetLastVertex(last));

            if (side != "none")
            {
                Near(side == "right" ? P(200, 70) : P(leftX, 70), mleader.GetLastVertex(0));
            }
        }

        [Fact]
        public void ALeaderOfNoLandingGivenAPointOnTheRight_MovesTheBlockFromThatPoint_AsAutoCadDoes()
        {
            // AutoCAD 2021: a leader added with no dogleg set, its line given (270, 80), then its last vertex (200, 70), moved the
            // box by (-70, -10) to (30, 40) (b2-k-right-addfirst-setlast).
            DxfMLeader mleader = OfBox(Drawing(), Points("none"));
            int line = Empty(mleader);

            mleader.AddFirstVertex(line, P(270, 80));
            mleader.SetLastVertex(line, P(200, 70));

            Near(P(30, 40), mleader.BlockPosition);
            Assert.Equal(1, mleader.VerticesCount(line));
        }

        // ---- the block's scale and turn set to what they are (item 3) ---------------------------------------------------------

        [Fact]
        public void TheBlockScaleAndRotationSetAsTheyAre_WriteTheMatrix_AsAutoCadDoes()
        {
            // AutoCAD 2021: the box moved to (130, 60), BlockScale 1 or BlockRotation 0 then set wrote its matrix as its transform
            // there (c3-k-moved-blockscale1, c3-k-moved-blockrot0); on a box just placed, at (100, 50) (c3-k-fresh-*).
            foreach (string which in new[] { "scale", "rotation" })
            {
                DxfMLeader mleader = OfBox(Drawing(), Points("left"));
                mleader.BlockPosition = P(130, 60);

                if (which == "scale")
                {
                    mleader.BlockScale = new GeoVector3(1, 1, 1);
                }
                else
                {
                    mleader.BlockRotation = 0;
                }

                NearAll(new[] { 1.0, 0, 0, 130, 0, 1, 0, 60 }, Matrix(mleader));
                Near(P(130, 60), mleader.BlockPosition);
                Assert.NotEqual(0, Flags(mleader) & (1 << (which == "scale" ? 21 : 22)));
            }

            DxfMLeader fresh = OfBox(Drawing(), Points("left"));
            fresh.BlockScale = new GeoVector3(1, 1, 1);
            NearAll(new[] { 1.0, 0, 0, 100, 0, 1, 0, 50 }, Matrix(fresh));
        }

        [Fact]
        public void TheBlockScaleSetAsItIs_SnapsALeaderBack_AndRedrawsATransformedBlock_AsAutoCadDoes()
        {
            // AutoCAD 2021: BlockScale 1 on the box of two leaders, the second off its landing at (40, 70), landed it from (142, 45)
            // (c3-k-two-unlanded-blockscale1); on the box scaled by 2 by TransformBy, it drew the box at 1, at (190, 105)
            // (c3-k-transform2-blockscale1). The library set the bit alone.
            DxfMLeader two = OfBox(Drawing(), Points("left"));
            SecondLeft(two);
            two.BlockScale = new GeoVector3(1, 1, 1);
            Near(P(160, 40), two.BlockPosition);
            Near(P(142, 45), two.GetLastVertex(1));

            DxfMLeader transformed = OfBox(Drawing(), Points("left"));
            transformed.TransformBy(GeoTransform3.Scaling(2.0));
            transformed.BlockScale = new GeoVector3(1, 1, 1);
            Near(P(190, 105), transformed.BlockPosition);
            Near(1, Number(After(transformed, "296", 16)));
            Near(P(164, 110), transformed.GetLastVertex(0));
        }

        [Theory]
        [InlineData(0.0, 90.0, -50.0, 100.0)]
        [InlineData(30.0, 120.0, -48.169872981077845, 105.49038105676658)]
        public void TheBlockRotationSetOnATurnedMultileader_TurnsTheBlockFromItsPlane_AsAutoCadDoes(double degrees, double drawn, double x, double y)
        {
            // AutoCAD 2021: on the box turned a quarter by TransformBy, BlockRotation 0 kept it drawn turned a quarter at (-50, 100)
            // (c3-k-rot90-blockrot0), and 30 degrees drew it turned 120 at (-48.17, 105.49) (k-rot90-blockrot30). The library drew
            // the second turned 30.
            DxfMLeader mleader = OfBox(Drawing(), Points("left"));
            mleader.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));

            mleader.BlockRotation = degrees * Math.PI / 180;

            Near(drawn * Math.PI / 180, Number(After(mleader, "296", 46)));
            Near(degrees * Math.PI / 180, mleader.BlockRotation);
            Near(P(x, y), mleader.BlockPosition);
        }

        // ---- the block moved and its matrix (item 4) ---------------------------------------------------------------------------

        [Fact]
        public void TheBlockMoved_LeavesTheMatrix_AsAutoCadLeavesIt()
        {
            // AutoCAD 2021: BlockPosition left the matrix as it was: none written yet, the identity (k-moved-right); written by
            // BlockScale 2 at (110, 45), there (d4-k-blockscale2-moved); so did a last vertex set, which moved the box to (172, 65)
            // (k-right2-base), and a dogleg of 5 after a scale of 2 (b-k-scale2-dl5).
            DxfMLeader moved = OfBox(Drawing(), Points("right"));
            moved.BlockPosition = P(130, 60);
            NearAll(new[] { 1.0, 0, 0, 0, 0, 1, 0, 0 }, Matrix(moved));

            DxfMLeader scaled = OfBox(Drawing(), Points("left"));
            scaled.BlockScale = new GeoVector3(2, 2, 2);
            scaled.BlockPosition = P(130, 60);
            NearAll(new[] { 2.0, 0, 0, 110, 0, 2, 0, 45 }, Matrix(scaled));

            DxfMLeader landed = OfBox(Drawing(), Points("right"));
            landed.SetLastVertex(0, P(200, 70));
            Near(P(172, 65), landed.BlockPosition);
            NearAll(new[] { 1.0, 0, 0, 0, 0, 1, 0, 0 }, Matrix(landed));

            DxfMLeader lengthened = OfBox(Drawing(), Points("left"));
            lengthened.Scale = 2;
            lengthened.DoglegLength = 5;
            Near(P(112, 45), lengthened.BlockPosition);
            NearAll(new[] { 2.0, 0, 0, 100, 0, 2, 0, 50 }, Matrix(lengthened));
        }

        // ---- a mirror across the XY plane (item 5) -----------------------------------------------------------------------------

        private static GeoTransform3 MirrorXY => GeoTransform3.Mirror(new GeoPlane3(P(0, 0), new GeoVector3(0, 0, 1)));

        [Fact]
        public void AMirrorAcrossTheXYPlane_TurnsTheBlocksNormalOver_ItsXScaleNegative_AsAutoCadWritesIt()
        {
            // AutoCAD 2021: the box mirrored across the XY plane was drawn where it was, its normal (0, 0, -1), its scales (-1, 1, 1),
            // its turn nought, its matrix that of the box at (100, 50) with z turned over; the plane's y axis (0, -1, 0) and 297 of
            // nought (e5-k-mirrorxy). The library kept its normal and scales and wrote 297 of 1.
            DxfMLeader mleader = OfBox(Drawing(), Points("left"));

            mleader.TransformBy(MirrorXY);

            Near(new GeoPoint3(0, 0, -1), Point(mleader, "296", 14));
            Near(new GeoPoint3(-1, 1, 1), Point(mleader, "296", 16));
            Near(0, Number(After(mleader, "296", 46)));
            NearAll(new[] { 1.0, 0, 0, 100, 0, 1, 0, 50, 0, 0, -1, 0 }, Matrix(mleader, 3));
            Near(P(0, -1), Point(mleader, "296", 112));
            Assert.Equal("0", After(mleader, "296", 297));
            Near(P(100, 50), mleader.BlockPosition);
            Near(P(82, 55), mleader.GetLastVertex(0));
            GeoTransform3 drawn = mleader.Explode().OfType<DxfBlockReference>().Single().BlockTransform;
            Near(P(90, 45), drawn.Transform(P(-10, -5)));
            Near(P(120, 65), drawn.Transform(P(20, 15)));
        }

        [Fact]
        public void AMirrorAcrossTheXYPlane_ThenTheBlocksScaleOrTurn_AsAutoCadDrawsThem()
        {
            // AutoCAD 2021: the box turned 30 degrees and mirrored was drawn turned 330 (e5-k-rot30-mirrorxy); BlockScale 2 after the
            // mirror drew it mirrored at (130, 45), from (90, 35) to (150, 75) (e5-k-mirrorxy-blockscale2); BlockRotation 30
            // degrees drew it turned 210 at (114.15, 56.83) (k-mirrorxy-blockrot30).
            DxfMLeader turned = OfBox(Drawing(), Points("left"), m => m.BlockRotation = Math.PI / 6);
            turned.TransformBy(MirrorXY);
            Near(5.759586531581287, Number(After(turned, "296", 46)));
            Near(Math.PI / 6, turned.BlockRotation);

            DxfMLeader scaled = OfBox(Drawing(), Points("left"));
            scaled.TransformBy(MirrorXY);
            scaled.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(130, 45), scaled.BlockPosition);
            NearAll(new[] { -2.0, 0, 0, 130, 0, 2, 0, 45 }, Matrix(scaled));
            GeoTransform3 drawn = scaled.Explode().OfType<DxfBlockReference>().Single().BlockTransform;
            Near(P(150, 35), drawn.Transform(P(-10, -5)));
            Near(P(90, 75), drawn.Transform(P(20, 15)));

            DxfMLeader rotated = OfBox(Drawing(), Points("left"));
            rotated.TransformBy(MirrorXY);
            rotated.BlockRotation = Math.PI / 6;
            Near(3.665191429188091, Number(After(rotated, "296", 46)));
            Near(P(114.15063509461096, 56.830127018922198), rotated.BlockPosition);
        }

        [Fact]
        public void AMirrorAcrossTheYZPlane_KeepsTheBlocksNormal_ItsXScaleNegative_AsAutoCadWritesIt()
        {
            // AutoCAD 2021: mirrored across the YZ plane, the box stood at (-100, 50), its normal (0, 0, 1), its scales (-1, 1, 1),
            // the plane's x axis (1, 0, 0) (g-k-mirroryz); BlockScale 2 then drew it unmirrored at (-100, 45) (g-k-mirroryz-blockscale2).
            DxfMLeader mleader = OfBox(Drawing(), Points("left"));

            mleader.TransformBy(GeoTransform3.Mirror(new GeoPlane3(P(0, 0), new GeoVector3(1, 0, 0))));

            Near(new GeoPoint3(0, 0, 1), Point(mleader, "296", 14));
            Near(new GeoPoint3(-1, 1, 1), Point(mleader, "296", 16));
            Near(P(1, 0), Point(mleader, "296", 111));
            Near(P(-100, 50), mleader.BlockPosition);

            mleader.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(-100, 45), mleader.BlockPosition);
            Near(P(-52, 55), mleader.GetLastVertex(0));
        }

        // ---- words given before the multileader is added (item 6) -------------------------------------------------------------

        [Fact]
        public void WordsOfATextStyleGivenBeforeTheMultileaderIsAdded_KeepIt_AsAutoCadKeepsThem()
        {
            // AutoCAD 2021: words of TS2 given to a multileader made in memory (of the drawing's defaults), then added, made it of
            // TS2 with bit 11 (f6-def-words-ts2); on a drawing whose current style's words are of TS2, of TS2 with no bit
            // (t-ts2-words-ts2). The library gave it the style's text style.
            DxfDatabase db = Drawing();
            var record = new DxfTextStyleTableRecord { Name = "TS2" };
            db.TextStyleTable.Add(record);
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0, TextStyleName = "TS2" } };

            db.ModelSpace.AppendEntity(mleader);

            Assert.Equal(record.Handle, After(mleader, "301", 343));
            Assert.Equal(record.Handle, After(mleader, "CONTEXT_DATA{", 340));
            Assert.NotEqual(0, Flags(mleader) & (1 << 11));
            Assert.Equal("TS2", mleader.MText.TextStyleName);
        }

        [Fact]
        public void WordsOfTheStylesTextStyleGivenBeforeTheMultileaderIsAdded_SetNoBit_AndWordsOfNoneTakeTheStyles()
        {
            // AutoCAD 2021: words of TS2 on a multileader whose style's words are of TS2 set no bit 11 (t-ts2-words-ts2). Words that
            // name no text style take the style's, as before.
            DxfDatabase db = Drawing();
            var record = new DxfTextStyleTableRecord { Name = "TS2" };
            db.TextStyleTable.Add(record);
            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");
            List<DxfTypedValue> tail = standard.Item.File.TailOf(standard.Item, 0);
            standard.Item.File.ReplaceTail(standard.Item, 0, tail.Select(g => new KeyValuePair<int, string>(g.TypeCode, g.TypeCode == 342 ? record.Handle : g.Value)).ToList());

            var same = new DxfMLeader { MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0, TextStyleName = "TS2" } };
            db.ModelSpace.AppendEntity(same);
            Assert.Equal(record.Handle, After(same, "301", 343));
            Assert.Equal(0, Flags(same) & (1 << 11));

            var none = new DxfMLeader { MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0 } };
            db.ModelSpace.AppendEntity(none);
            Assert.Equal(record.Handle, After(none, "301", 343));
            Assert.Equal(record.Handle, After(none, "CONTEXT_DATA{", 340));
            Assert.Equal(0, Flags(none) & (1 << 11));
        }
    }
}
