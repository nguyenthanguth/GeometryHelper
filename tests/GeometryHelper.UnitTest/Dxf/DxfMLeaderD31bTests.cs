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
    /// The leftovers of the landing of a MULTILEADER as AutoCAD 2021 makes and edits it (plugin D31b, rounds A and B, on a
    /// Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8): a block's landing, the content moved to keep the
    /// landings, the underline of attachment 8, the style's linetype and arrow, the arrows set to the size they have, and the
    /// scale of words of a height and of columns.
    /// </summary>
    public class DxfMLeaderD31bTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        // AutoCAD 2021's records (after AcDbMLeader, its proxy graphics left out) of words "ABC" 4 high held at (100, 50): a line
        // from (250, 0) and its leader on the right, its dogleg of 8 from (120.67, 48) (d02) ...
        private const string Right =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... a line from (0, 0) on the left too, its dogleg from (90, 48) (d03) ...
        private const string Both =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... and three lines of words "A\PBB\PCCC" met on the right (d05).
        private const string RightThree =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 A\\PBB\\PCCC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 33850368|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        private const string Three = "A\\PBB\\PCCC";

        private static DxfDatabase Drawing() => DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Near(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= 1E-6,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

        /// <summary>
        /// A multileader of the drawing given AutoCAD 2021's record: one made and added, then its groups after its AcDbMLeader
        /// marker replaced by AutoCAD's, its style (AutoCAD's 67) and text style (55) the drawing's, its linetype (5B) left out.
        /// </summary>
        private static DxfMLeader AutoCads(DxfDatabase db, string record)
        {
            var made = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "X",
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 4.0,
                },
            };
            db.ModelSpace.AppendEntity(made);
            List<DxfTypedValue> tail = made.Item.File.TailOf(made.Item, 0);
            int marker = tail.FindIndex(g => g.TypeCode == 100 && g.Value.Trim() == "AcDbMLeader");
            int close = tail.FindIndex(g => g.TypeCode == 301);
            string style = tail.Skip(close).First(g => g.TypeCode == 340).Value.Trim();
            string text = tail.First(g => g.TypeCode == 343).Value.Trim();
            var groups = tail.Take(marker + 1).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();

            foreach (string pair in record.Split('|'))
            {
                int space = pair.IndexOf(' ');
                int code = int.Parse(pair.Substring(0, space), CultureInfo.InvariantCulture);
                string value = pair.Substring(space + 1);

                if (code == 341)
                {
                    continue;
                }

                value =
                    code == 340 && value == "67" ? style
                    : (code == 340 || code == 343) && value == "55" ? text
                    : value;
                groups.Add(new KeyValuePair<int, string>(code, value));
            }

            made.Item.File.ReplaceTail(made.Item, 0, groups);
            return made;
        }

        /// <summary>A multileader made in code as GHD31B made AutoCAD's: words 4 high held at (100, 50), lines from the points given (from (0, 0) where none is).</summary>
        private static DxfMLeader Words(DxfDatabase db, string words = "ABC", params GeoPoint3[] points)
        {
            var mleader = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = words,
                    Location = P(100, 50),
                    TextHeight = 4.0,
                },
            };

            foreach (GeoPoint3 point in points.Length == 0 ? new[] { P(0, 0) } : points)
            {
                mleader.AddLeaderLine(point);
            }

            db.ModelSpace.AppendEntity(mleader);
            return mleader;
        }

        /// <summary>
        /// A block of GHD31B's: BOX the lines of the box from (-10, -5) to (20, 15); TAG a circle of radius 10 about the origin and a
        /// line from (-10, 0) to (20, 5); SLANT the line from (0, -10) to (20, 10); TWO circles of radius 3 about (0, 10) and (20,
        /// -10); ORIGIN the box of BOX, the block's origin at (5, 5).
        /// </summary>
        private static DxfBlockTableRecord Block(DxfDatabase db, string kind)
        {
            var block = new DxfBlockTableRecord { Name = "GH_" + kind };
            db.BlockTable.Add(block);

            if (kind == "ORIGIN")
            {
                block.Origin = new GeoPoint3(5, 5, 0);
            }

            var z = new GeoVector3(0, 0, 1);

            switch (kind)
            {
                case "TAG":
                    block.AppendEntity(new DxfCircle(P(0, 0), z, 10));
                    block.AppendEntity(new DxfLine(P(-10, 0), P(20, 5)));
                    break;
                case "SLANT":
                    block.AppendEntity(new DxfLine(P(0, -10), P(20, 10)));
                    break;
                case "TWO":
                    block.AppendEntity(new DxfCircle(P(0, 10), z, 3));
                    block.AppendEntity(new DxfCircle(P(20, -10), z, 3));
                    break;
                default:
                    block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
                    block.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));
                    block.AppendEntity(new DxfLine(P(20, 15), P(-10, 15)));
                    block.AppendEntity(new DxfLine(P(-10, 15), P(-10, -5)));
                    break;
            }

            return block;
        }

        /// <summary>A multileader of a block of GHD31B's placed at (100, 50), as GHD31B made AutoCAD's, before its leaders.</summary>
        private static DxfMLeader Of(DxfDatabase db, string kind, Action<DxfMLeader> before = null)
        {
            DxfBlockTableRecord block = Block(db, kind);
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.BlockContent = block;
            mleader.BlockPosition = P(100, 50);
            before?.Invoke(mleader);
            return mleader;
        }

        /// <summary>The text of the first group of a code in the multileader's record after the first group of a value (as <c>LEADER{</c>) or code; null where it has none.</summary>
        private static string After(DxfEntity entity, string from, int code, int skip = 0)
        {
            List<DxfTypedValue> tail = entity.Item.File.TailOf(entity.Item, 0);
            int start = tail.FindIndex(g => int.TryParse(from, out int at) ? g.TypeCode == at : g.Value.Trim() == from);
            return tail.Skip(start + 1)
                .Where(g => g.TypeCode == code)
                .Skip(skip)
                .Select(g => g.Value.Trim())
                .FirstOrDefault();
        }

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        // ---- a block's landing (item 2) ------------------------------------------------------------------------------------

        [Theory]
        [InlineData("BOX", 0.0, false, 82.0, 55.0, 1.0)]
        [InlineData("BOX", 250.0, false, 128.0, 55.0, -1.0)]
        [InlineData("BOX", 0.0, true, 82.0, 50.0, 1.0)]
        [InlineData("BOX", 250.0, true, 128.0, 50.0, -1.0)]
        [InlineData("BOX", 103.0, false, 82.0, 55.0, 1.0)]
        [InlineData("BOX", 107.0, false, 128.0, 55.0, -1.0)]
        [InlineData("TAG", 0.0, false, 82.0, 50.0, 1.0)]
        [InlineData("TAG", 250.0, false, 118.0, 50.0, -1.0)]
        [InlineData("TAG", 103.0, false, 82.0, 50.0, 1.0)]
        [InlineData("SLANT", 0.0, false, 102.0, 50.0, 1.0)]
        [InlineData("SLANT", 250.0, false, 118.0, 50.0, -1.0)]
        [InlineData("TWO", 0.0, false, 102.0, 50.0, 1.0)]
        [InlineData("TWO", 250.0, true, 108.0, 50.0, -1.0)]
        [InlineData("ORIGIN", 0.0, false, 77.0, 50.0, 1.0)]
        [InlineData("ORIGIN", 250.0, true, 123.0, 50.0, -1.0)]
        public void ALeaderAddedToABlock_LandsAtTheSideOfItsShapes_AsAutoCadLandsIt(
            string kind,
            double from,
            bool atBase,
            double x,
            double y,
            double along
        )
        {
            // AutoCAD 2021 (D31b, k-): the dogleg ends where the block's shapes meet the line along x through the middle of its
            // box (or its position, at its base), the outermost on the side of the point, else at that line's point; no gap. The
            // side is the one of the box's middle the point is on: 103 under a box from 90 to 120 lands left, 107 right. The
            // library made the leader at the base point of the content, its dogleg along x whatever the side.
            DxfMLeader mleader = Of(Drawing(), kind, m =>
            {
                if (atBase)
                {
                    m.BlockConnectionType = DxfBlockConnectionType.ConnectBase;
                }
            });

            mleader.AddLeaderLine(P(from, 0));

            Near(P(x, y), mleader.GetLastVertex(0));
            GeoVector3 dogleg = mleader.GetDogleg(0);
            Near(P(along, 0), new GeoPoint3(dogleg.X, dogleg.Y, dogleg.Z));
        }

        [Fact]
        public void ALeaderAddedToABlockScaledAndTurned_MeetsItsTurnedSides_AsAutoCadLandsIt()
        {
            // AutoCAD 2021 (k-BOX-turned): BOX scaled by 2 and turned by 30 degrees has the box (67.68, 31.34) to (139.64, 95.98);
            // its leaders end at x 69.02 and 138.30 at y 63.66, their doglegs from 61.02 and 146.30. The scale and turn set before
            // the block, as GHD31B set them: set after it is placed, they move it (D31d, k-placed-blockscale2-left).
            DxfMLeader left = Turned(Drawing());
            DxfMLeader right = Turned(Drawing());

            left.AddLeaderLine(P(0, 0));
            right.AddLeaderLine(P(250, 0));

            Near(P(61.01923788646684, 63.660254037844389), left.GetLastVertex(0));
            Near(P(146.30127018922195, 63.660254037844389), right.GetLastVertex(0));
        }

        /// <summary>A multileader of BOX scaled by 2 and turned by 30 degrees before it is given the block, placed at (100, 50), as GHD31B made AutoCAD's.</summary>
        private static DxfMLeader Turned(DxfDatabase db)
        {
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.BlockScale = new GeoVector3(2, 2, 2);
            mleader.BlockRotation = Math.PI / 6;
            mleader.BlockContent = Block(db, "BOX");
            mleader.BlockPosition = P(100, 50);
            return mleader;
        }

        [Fact]
        public void TheBlocksConnectionSet_IsWrittenInTheContextDataToo_WhereAutoCadReadsIt()
        {
            // AutoCAD 2021 wrote ConnectBase in 176 and in 177 of the context data (k-BOX-left-base), and read the library's
            // multileaders of 176 alone as connected at their extents (D31b, i).
            DxfMLeader mleader = Of(Drawing(), "BOX");
            mleader.BlockConnectionType = DxfBlockConnectionType.ConnectBase;

            Assert.Equal("1", After(mleader, "CONTEXT_DATA{", 177));
            Assert.Equal("1", After(mleader, "301", 176));
        }

        [Fact]
        public void LeadersFromBothSidesOfABlock_LandOnTheirOwnSides_AsAutoCadLandsThem()
        {
            // AutoCAD 2021 (k-BOX-both): leader 0 from (82, 55) along x, leader 1 from (128, 55) against it.
            DxfMLeader mleader = Of(Drawing(), "BOX");
            mleader.AddLeaderLine(P(0, 0));
            mleader.AddLeaderLine(P(250, 0));

            Assert.Equal(2, mleader.LeaderCount);
            Near(P(82, 55), mleader.GetLastVertex(0));
            Near(P(128, 55), mleader.GetLastVertex(1));
        }

        // ---- the content moved to keep the landings (item 3) --------------------------------------------------------------

        [Fact]
        public void TheGapSetOnWordsMetOnTheRight_MovesTheWordsAndTheLanding_AsAutoCadMovesThem()
        {
            // AutoCAD 2021 (m-right-gap5, m2-noleader-gap5, g7): a gap of 5 for 2 moved the words from (100, 50) to (103, 50) and
            // the dogleg on their right from (120.67, 48) to (126.67, 48), with or without a leader on the left, which stayed.
            DxfMLeader right = AutoCads(Drawing(), Right);
            right.LandingGap = 5;
            Near(P(103, 50), right.TextLocation);
            Near(P(126.66666666666666, 48), right.GetLastVertex(0));

            DxfMLeader both = AutoCads(Drawing(), Both);
            both.LandingGap = 5;
            Near(P(103, 50), both.TextLocation);
            Near(P(90, 48), both.GetLastVertex(0));
            Near(P(126.66666666666666, 48), both.GetLastVertex(1));

            DxfMLeader none = Words(Drawing());
            none.RemoveLeader(none.GetLeaderIndexes()[0]);
            none.LandingGap = 5;
            Near(P(103, 50), none.TextLocation);
        }

        [Fact]
        public void TheDoglegSetOnLeadersOfOneSide_MovesTheWords_AndOnBothSidesLandsTheLeadersAnew_AsAutoCadDoes()
        {
            // AutoCAD 2021: a dogleg of 15 moved words met on the right to (93, 50) (m-right-dl15), words met by two leaders on
            // the left to (107, 50) (m2-twoleft-dl15), and kept words met on both sides, landing the doglegs anew from (83, 48)
            // and (127.67, 48) (m2-both-dl15); SetDoglegLength(1, 15) on both sides moved the right one alone (m2-both-setdl1).
            DxfMLeader right = AutoCads(Drawing(), Right);
            right.DoglegLength = 15;
            Near(P(93, 50), right.TextLocation);
            Near(P(120.66666666666666, 48), right.GetLastVertex(0));

            DxfMLeader twoLeft = Words(Drawing(), "ABC", P(0, 0), P(0, 100));
            twoLeft.DoglegLength = 15;
            Near(P(107, 50), twoLeft.TextLocation);
            Near(P(90, 48), twoLeft.GetLastVertex(1));

            DxfMLeader both = AutoCads(Drawing(), Both);
            both.DoglegLength = 15;
            Near(P(100, 50), both.TextLocation);
            Near(P(83, 48), both.GetLastVertex(0));
            Near(P(127.66666666666666, 48), both.GetLastVertex(1));

            DxfMLeader one = AutoCads(Drawing(), Both);
            one.SetDoglegLength(1, 15);
            Near(P(100, 50), one.TextLocation);
            Near(P(90, 48), one.GetLastVertex(0));
            Near(P(127.66666666666666, 48), one.GetLastVertex(1));

            DxfMLeader left = Words(Drawing());
            left.SetDoglegLength(0, 15);
            Near(P(107, 50), left.TextLocation);
        }

        [Fact]
        public void TheLastVertexSet_CarriesTheWordsAndTheOtherLandings_AsAutoCadCarriesThem()
        {
            // AutoCAD 2021: SetVertex(0, 1, (50, 60)) moved words met on the left to (60, 62) (m-left-setvertex); SetLastVertex
            // of the right line to (200, 60) moved words met on the right to (179.33, 62) (m-right-setlast), and, met on both
            // sides, the left dogleg with them to (169.33, 60) (m-both-setlast1).
            DxfMLeader left = Words(Drawing());
            left.SetVertex(0, 1, P(50, 60));
            Near(P(60, 62), left.TextLocation);

            DxfMLeader right = AutoCads(Drawing(), Right);
            right.SetLastVertex(0, P(200, 60));
            Near(P(179.33333333333331, 62), right.TextLocation);

            DxfMLeader both = AutoCads(Drawing(), Both);
            both.SetLastVertex(1, P(200, 60));
            Near(P(179.33333333333331, 62), both.TextLocation);
            Near(P(169.33333333333331, 60), both.GetLastVertex(0));
            Near(P(200, 60), both.GetLastVertex(1));
        }

        [Fact]
        public void TheHeightAndScaleSetOnWordsMetOnTheLeft_MoveTheWords_AsAutoCadMovesThem()
        {
            // AutoCAD 2021: a height of 10 moved words met on the left from (100, 50) to (100, 53) (c08b, m2-left-mtext-h10);
            // Scale 2 to (110, 52), the base point of the content to (106, 48) (d12).
            DxfMLeader high = Words(Drawing());
            high.TextHeight = 10;
            Near(P(100, 53), high.TextLocation);
            Near(P(90, 48), high.GetLastVertex(0));

            DxfMLeader scaled = Words(Drawing());
            scaled.Scale = 2;
            Near(P(110, 52), scaled.TextLocation);
            Near(P(90, 48), scaled.GetLastVertex(0));
        }

        [Fact]
        public void TheAttachmentOfASideSet_MovesTheWordsOrLandsTheLeadersAnew_AsAutoCadDoes()
        {
            // AutoCAD 2021, three lines met at the middle of their top line: the left made the bottom of the bottom line moved the
            // words to (100, 65.33) and the right dogleg to (120.67, 63.33) (m2-both-attL4); the right made so landed the right
            // dogleg anew at (120.67, 32.67) (m2-both-attR4), and with no leader on the left moved the words (m-right-att4).
            DxfMLeader left = AutoCads(Drawing(), Both);
            left.MText = new DxfMText
            {
                Contents = Three,
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            left.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentBottomOfBottom,
                DxfLeaderDirectionType.LeftLeader
            );
            Near(P(100, 65.333333333333329), left.TextLocation);
            Near(P(90, 48), left.GetLastVertex(0));
            Near(P(120.66666666666666, 63.333333333333329), left.GetLastVertex(1));

            DxfMLeader right = AutoCads(Drawing(), Both);
            right.MText = new DxfMText
            {
                Contents = Three,
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            right.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentBottomOfBottom,
                DxfLeaderDirectionType.RightLeader
            );
            Near(P(100, 50), right.TextLocation);
            Near(P(120.66666666666666, 32.666666666666671), right.GetLastVertex(1));

            DxfMLeader alone = AutoCads(Drawing(), RightThree);
            alone.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentBottomOfBottom,
                DxfLeaderDirectionType.RightLeader
            );
            Near(P(100, 65.333333333333329), alone.TextLocation);
            Near(P(120.66666666666666, 48), alone.GetLastVertex(0));
        }

        [Fact]
        public void WordsGivenAnew_KeepTheirLanding_AsAutoCadKeepsThem()
        {
            // AutoCAD 2021: words given anew at (120, 80) stayed at (100, 50) (m-left-mtext-moved, m2-right-mtext-moved); three
            // lines met at their middle stood at (100, 56.67) (m2-left-mtext-three-att2).
            DxfMLeader left = Words(Drawing());
            DxfMText moved = left.MText;
            moved.Location = P(120, 80);
            left.MText = moved;
            Near(P(100, 50), left.TextLocation);

            DxfMLeader right = AutoCads(Drawing(), Right);
            DxfMText movedRight = right.MText;
            movedRight.Location = P(120, 80);
            right.MText = movedRight;
            Near(P(100, 50), right.TextLocation);

            DxfMLeader middle = Words(Drawing());
            middle.TextAttachmentType = DxfTextAttachmentType.AttachmentMiddle;
            middle.MText = new DxfMText
            {
                Contents = Three,
                Location = P(120, 80),
                TextHeight = 4.0,
            };
            Near(P(100, 56.666666666666664), middle.TextLocation);
        }

        [Fact]
        public void TheDoglegOrLastVertexOfABlockSet_MovesTheBlock_AsAutoCadMovesIt()
        {
            // AutoCAD 2021: a dogleg of 15 moved BOX met on the left to (107, 50) (k2-BOX-dl15), and SetLastVertex(0, (50, 60))
            // to (68, 55) (k2-BOX-setlast); a gap of 5 left it (k2-BOX-gap5).
            DxfMLeader longer = Of(Drawing(), "BOX");
            longer.AddLeaderLine(P(0, 0));
            longer.DoglegLength = 15;
            Near(P(107, 50), longer.BlockPosition);

            DxfMLeader set = Of(Drawing(), "BOX");
            set.AddLeaderLine(P(0, 0));
            set.SetLastVertex(0, P(50, 60));
            Near(P(68, 55), set.BlockPosition);
            Near(P(68, 55), set.Explode().OfType<DxfBlockReference>().Single().Position);

            DxfMLeader gap = Of(Drawing(), "BOX");
            gap.AddLeaderLine(P(0, 0));
            gap.LandingGap = 5;
            Near(P(100, 50), gap.BlockPosition);
        }

        // ---- the underline of attachment 8 (item 4) ------------------------------------------------------------------------

        [Fact]
        public void Attachment8SetOnWordsMetOnTheLeft_UnderlinesThemAndLiftsThem_AsAutoCadDoes()
        {
            // AutoCAD 2021 (u-att8-after): the words "ABC" became "\LABC" and moved to (100, 52.8), the dogleg kept; set back to the
            // middle of the top line (u-att8-back1), "ABC" again.
            DxfMLeader mleader = Words(Drawing());
            mleader.SetTextAttachmentType(DxfTextAttachmentType.AttachmentAllLine, DxfLeaderDirectionType.LeftLeader);

            Assert.Equal("\\LABC", After(mleader, "CONTEXT_DATA{", 304));
            Near(P(100, 52.8), mleader.TextLocation);
            Near(P(90, 48), mleader.GetLastVertex(0));

            mleader.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentMiddleOfTop,
                DxfLeaderDirectionType.LeftLeader
            );
            Assert.Equal("ABC", After(mleader, "CONTEXT_DATA{", 304));
            Near(P(100, 50), mleader.TextLocation);
        }

        [Fact]
        public void Attachment8_UnderlinesWordsOnceAndOnlyWhereALeaderMeetsThatSide_AsAutoCadDoes()
        {
            // AutoCAD 2021: words "\LABC" stayed "\LABC" (u-att8-underlined); new words "XYZ" became "\LXYZ" (u-att8-newwords);
            // the right made 8 with a leader on the left alone left "ABC" (u-att8-right-only); words of no letters stayed so
            // (u-att8-empty); attachment 8 given before the words and the leader underlined them as the leader came (d04).
            DxfMLeader underlined = Words(Drawing(), "\\LABC");
            underlined.TextAttachmentType = DxfTextAttachmentType.AttachmentAllLine;
            Assert.Equal("\\LABC", After(underlined, "CONTEXT_DATA{", 304));

            DxfMLeader renewed = Words(Drawing());
            renewed.SetTextAttachmentType(DxfTextAttachmentType.AttachmentAllLine, DxfLeaderDirectionType.LeftLeader);
            renewed.MText = new DxfMText
            {
                Contents = "XYZ",
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            Assert.Equal("\\LXYZ", After(renewed, "CONTEXT_DATA{", 304));

            DxfMLeader rightOnly = Words(Drawing());
            rightOnly.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentAllLine,
                DxfLeaderDirectionType.RightLeader
            );
            Assert.Equal("ABC", After(rightOnly, "CONTEXT_DATA{", 304));

            DxfMLeader empty = Words(Drawing(), string.Empty);
            empty.SetTextAttachmentType(DxfTextAttachmentType.AttachmentAllLine, DxfLeaderDirectionType.LeftLeader);
            Assert.Equal(string.Empty, After(empty, "CONTEXT_DATA{", 304) ?? string.Empty);

            var before = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = Three,
                    Location = P(100, 50),
                    TextHeight = 4.0,
                },
            };
            before.SetTextAttachmentType(DxfTextAttachmentType.AttachmentAllLine, DxfLeaderDirectionType.LeftLeader);
            before.AddLeaderLine(P(0, 0));
            Drawing().ModelSpace.AppendEntity(before);
            Assert.Equal("\\L" + Three, After(before, "CONTEXT_DATA{", 304));
        }

        // ---- the style's linetype and arrow (item 5) -----------------------------------------------------------------------

        [Fact]
        public void ANewMultileader_TakesItsStylesLinetypeAndArrowBlock_AsAutoCadWritesThem()
        {
            // AutoCAD 2021 (l-standard, l-s2-current): a new multileader of Standard wrote 341 of the style's linetype (its 340,
            // ByBlock), and of a style with an arrow block 342 of that block (the style's 341). The library wrote neither, and
            // AutoCAD drew such a multileader's leader lines ByLayer (D31, i).
            DxfDatabase plain = Drawing();
            string byBlock = plain.LinetypeTable["ByBlock"].Handle;
            string text = DxfHatchLeaderDrawings.R2018.Replace(
                "AcDbMLeaderStyle\n179\n2\n",
                "AcDbMLeaderStyle\n179\n2\n340\n" + byBlock + "\n341\nB5\n"
            );
            Assert.NotEqual(DxfHatchLeaderDrawings.R2018, text);
            DxfDatabase db = DxfDatabase.Parse(text);

            DxfMLeader mleader = Words(db);

            Assert.Equal(byBlock, After(mleader, "301", 341));
            Assert.Equal("B5", After(mleader, "301", 342));
            Assert.Equal("ByBlock", mleader.Explode().OfType<DxfPolyline>().First().Linetype);
        }

        // ---- the arrows set to the size they have (item 6) -----------------------------------------------------------------

        [Fact]
        public void TheArrowSizeSet_IsWrittenTimesTheScale_EvenWhereItIsTheSizeItHas_AsAutoCadWritesIt()
        {
            // AutoCAD 2021: ArrowSize 4 on arrows of 4 set the bit of the overrides 9 (a-same); 5 at a scale of 2 wrote 42 of 5 and
            // 140 of 10 (a-scale2-five); 4 after TransformBy by 2 drew the arrows 4 again, 140 from 8 to 4 (a-transform2-same).
            DxfMLeader same = Words(Drawing());
            int flags = int.Parse(After(same, "301", 90), CultureInfo.InvariantCulture);
            same.ArrowSize = 4;
            Assert.Equal(flags | (1 << 9), int.Parse(After(same, "301", 90), CultureInfo.InvariantCulture));

            DxfMLeader scaled = Words(Drawing());
            scaled.Scale = 2;
            scaled.ArrowSize = 5;
            Assert.Equal(5.0, Number(After(scaled, "301", 42)));
            Assert.Equal(10.0, Number(After(scaled, "CONTEXT_DATA{", 140)));

            DxfMLeader moved = Words(Drawing());
            moved.TransformBy(GeoTransform3.Scaling(2.0));
            Assert.Equal(8.0, Number(After(moved, "CONTEXT_DATA{", 140)));
            moved.ArrowSize = 4;
            Assert.Equal(4.0, Number(After(moved, "CONTEXT_DATA{", 140)));
        }

        // ---- the scale of words of a height and of columns (item 7) --------------------------------------------------------

        [Fact]
        public void TheScaleSet_ScalesTheHeightOfTheWords_AndTheirWidthWhereTheyHaveNoColumns_AsAutoCadScalesThem()
        {
            // AutoCAD 2021: Scale 2 on words 60 wide and 30 high wrote 43 of 120 and 44 of 60 (s-defh-scale2); on words of two
            // static columns 30 wide with a gutter of 5, 65 wide and 12 high, 43 of 65, 44 of 24, 142 of 30 and 143 of 5
            // (s-static-scale2); dynamic ones the same (s2-dynamic-auto-scale2). The library left 44.
            DxfMLeader plain = Words(Drawing(), Three);
            plain.MText = new DxfMText
            {
                Contents = Three,
                Location = P(100, 50),
                TextHeight = 4.0,
                Width = 60,
            };
            SetGroup(plain, 44, "30.0");
            plain.Scale = 2;
            Assert.Equal(120.0, Number(After(plain, "CONTEXT_DATA{", 43)));
            Assert.Equal(60.0, Number(After(plain, "CONTEXT_DATA{", 44)));

            DxfMLeader columns = Words(Drawing(), Three);
            SetGroup(columns, 43, "65.0");
            SetGroup(columns, 44, "12.0");
            SetGroup(columns, 173, "1");
            SetGroup(columns, 142, "30.0");
            SetGroup(columns, 143, "5.0");
            columns.Scale = 2;
            Assert.Equal(65.0, Number(After(columns, "CONTEXT_DATA{", 43)));
            Assert.Equal(24.0, Number(After(columns, "CONTEXT_DATA{", 44)));
            Assert.Equal(30.0, Number(After(columns, "CONTEXT_DATA{", 142)));
            Assert.Equal(5.0, Number(After(columns, "CONTEXT_DATA{", 143)));
        }

        /// <summary>Writes a group of the multileader's words (the first of its code after <c>CONTEXT_DATA{</c>) as text.</summary>
        private static void SetGroup(DxfMLeader mleader, int code, string value)
        {
            List<DxfTypedValue> tail = mleader.Item.File.TailOf(mleader.Item, 0);
            int start = tail.FindIndex(g => g.Value.Trim() == "CONTEXT_DATA{");
            int at = tail.FindIndex(start, g => g.TypeCode == code);
            var groups = tail.Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            groups[at] = new KeyValuePair<int, string>(code, value);
            mleader.Item.File.ReplaceTail(mleader.Item, 0, groups);
        }
    }
}
