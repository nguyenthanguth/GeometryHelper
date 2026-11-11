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
    /// The block of a MULTILEADER placed anew as AutoCAD 2021 places it, and the leftovers of D31c (plugin D31d, rounds C and D,
    /// on a Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8): the block's scale and turn set after a leader, its
    /// place set, a multileader of a block transformed, words given with their text style, leaders off their landing snapped
    /// back, and several leaders on one side under the scale.
    /// </summary>
    public class DxfMLeaderD31dTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        // AutoCAD 2021's records (after AcDbMLeader, its proxy graphics left out) of words "ABC" 4 high at (160, 40) met on the
        // left by two leaders: the first landed at (150, 38), the second's dogleg moved off to (40, 70) (D31c s2-two-base) ...
        private const string TwoUnlanded =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 158.0|20 38.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 160.0|22 40.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 150.0|20 38.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 40.0|20 70.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... both landed there (D31c b-two-landed) ...
        private const string TwoLanded =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 158.0|20 38.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 160.0|22 40.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 150.0|20 38.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 150.0|20 38.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 296064|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... words at (-10, 40) met on the right by a leader landed at (10.67, 38), on the left by one moved off to (160, 70)
        // (D31c d6-tworight-base) ...
        private const string RightAndUnlanded =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 -12.0|20 38.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 -10.0|22 40.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 10.66666666666667|20 38.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 160.0|20 70.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... the box of BOX (block 78) at (160, 40), met on the left by two leaders: the first landed at (142, 45), the second's
        // dogleg moved off to (40, 70) (d-k-two-unlanded-base) ...
        private const string BlockTwoUnlanded =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 150.0|20 45.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 0|296 1|341 78|14 0.0|24 0.0|34 1.0|15 160.0|25 40.0|35 0.0|16 1.0|26 1.0|36 1.0|46 0.0|93 -1073741824|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 142.0|20 45.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 40.0|20 70.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 525312|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 1|343 55|173 1|95 1|174 1|175 0|92 -1056964608|292 0|344 78|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... both landed there (c-k-twoleft-base) ...
        private const string BlockTwoLanded =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 150.0|20 45.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 0|296 1|341 78|14 0.0|24 0.0|34 1.0|15 160.0|25 40.0|35 0.0|16 1.0|26 1.0|36 1.0|46 0.0|93 -1073741824|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 142.0|20 45.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 142.0|20 45.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 525440|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 1|343 55|173 1|95 1|174 1|175 0|92 -1056964608|292 0|344 78|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... and the box at (172, 65) met on the right by two leaders landed at (200, 70) (d-k-tworight-base).
        private const string BlockTwoRight =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 162.0|20 70.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 0|296 1|341 78|14 0.0|24 0.0|34 1.0|15 172.0|25 65.0|35 0.0|16 1.0|26 1.0|36 1.0|46 0.0|93 -1073741824|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|47 0.0|47 0.0|47 0.0|47 0.0|47 1.0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 200.0|20 70.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 200.0|20 70.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|10 270.0|20 80.0|30 0.0|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 525440|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 1|343 55|173 1|95 1|174 1|175 0|92 -1056964608|292 0|344 78|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

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

        private static void NearAll(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], actual[i]);
            }
        }

        /// <summary>
        /// A multileader of the drawing given AutoCAD 2021's record: one made and added, then its groups after its AcDbMLeader
        /// marker replaced by AutoCAD's, its style (AutoCAD's 67), text style (55) and block (78, BOX of <see cref="Block"/>) the
        /// drawing's, its linetype (5B) left out.
        /// </summary>
        private static DxfMLeader AutoCads(DxfDatabase db, string record)
        {
            string box = record.Contains("|344 78|") ? Block(db, "BOX").Handle : null;
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

                if (code == 341 && value != "78")
                {
                    continue;
                }

                value =
                    code == 340 && value == "67" ? style
                    : (code == 340 || code == 343) && value == "55" ? text
                    : (code == 341 || code == 344) && value == "78" ? box
                    : value;
                groups.Add(new KeyValuePair<int, string>(code, value));
            }

            made.Item.File.ReplaceTail(made.Item, 0, groups);
            return made;
        }

        /// <summary>
        /// A block of GHD31D's: BOX the lines of the box from (-10, -5) to (20, 15); TAG a circle of radius 10 about the origin and a
        /// line from (-10, 0) to (20, 5); ORIGIN the box of BOX, the block's origin at (5, 5).
        /// </summary>
        private static DxfBlockTableRecord Block(DxfDatabase db, string kind)
        {
            var block = new DxfBlockTableRecord { Name = "GH_" + kind };
            db.BlockTable.Add(block);

            if (kind == "ORIGIN")
            {
                block.Origin = new GeoPoint3(5, 5, 0);
            }

            if (kind == "TAG")
            {
                block.AppendEntity(new DxfCircle(P(0, 0), new GeoVector3(0, 0, 1), 10));
                block.AppendEntity(new DxfLine(P(-10, 0), P(20, 5)));
            }
            else
            {
                block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
                block.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));
                block.AppendEntity(new DxfLine(P(20, 15), P(-10, 15)));
                block.AppendEntity(new DxfLine(P(-10, 15), P(-10, -5)));
            }

            return block;
        }

        /// <summary>
        /// A multileader of a block of GHD31D's placed at (100, 50), as GHD31D made AutoCAD's: set before it is placed, then after
        /// (before its leaders), then its lines from the points given.
        /// </summary>
        private static DxfMLeader Of(
            DxfDatabase db,
            string kind,
            GeoPoint3[] points,
            Action<DxfMLeader> before = null,
            Action<DxfMLeader> placed = null
        )
        {
            DxfBlockTableRecord block = Block(db, kind);
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            before?.Invoke(mleader);
            mleader.BlockContent = block;
            mleader.BlockPosition = P(100, 50);
            placed?.Invoke(mleader);

            foreach (GeoPoint3 point in points)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        private static GeoPoint3[] Points(string side) =>
            side == "left" ? new[] { P(0, 0) }
            : side == "right" ? new[] { P(250, 0) }
            : side == "both" ? new[] { P(0, 0), P(250, 0) }
            : new GeoPoint3[0];

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

        private static int Flags(DxfMLeader mleader) =>
            int.Parse(After(mleader, "301", 90), CultureInfo.InvariantCulture);

        /// <summary>The first two rows of the block's matrix (its 47 groups), as written.</summary>
        private static double[] Matrix(DxfMLeader mleader) =>
            Enumerable.Range(0, 8).Select(i => Number(After(mleader, "296", 47, i))).ToArray();

        /// <summary>The base point of the content (10 of the context data).</summary>
        private static GeoPoint3 BasePoint(DxfMLeader mleader) =>
            P(Number(After(mleader, "CONTEXT_DATA{", 10)), Number(After(mleader, "CONTEXT_DATA{", 20)));

        // ---- the block's scale and turn set after a leader (item 1) ------------------------------------------------------------

        [Theory]
        [InlineData("left")]
        [InlineData("right")]
        [InlineData("both")]
        [InlineData("none")]
        public void TheBlockScaleSet_MovesTheBlockSoItIsMetAtTheBasePoint_AndLandsTheLeaders_AsAutoCadDoes(string side)
        {
            // AutoCAD 2021: BlockScale set to 2 on the box at (100, 50), met at (90, 55) on its left, moved it to (110, 45), so that
            // it is met there still, its matrix its transform there, and the leaders landed anew: the left from (82, 55), the
            // right from (158, 55), with leaders on one side, both or none (b-k-blockscale2-*). The library left it at (100, 50).
            DxfMLeader mleader = Of(Drawing(), "BOX", Points(side));

            mleader.BlockScale = new GeoVector3(2, 2, 2);

            Near(P(110, 45), mleader.BlockPosition);
            Near(P(90, 55), BasePoint(mleader));
            NearAll(new[] { 2.0, 0, 0, 110, 0, 2, 0, 45 }, Matrix(mleader));
            int line = 0;

            if (side == "left" || side == "both")
            {
                Near(P(82, 55), mleader.GetLastVertex(line++));
            }

            if (side == "right" || side == "both")
            {
                Near(P(158, 55), mleader.GetLastVertex(line));
            }
        }

        [Theory]
        [InlineData("left")]
        [InlineData("right")]
        public void TheBlockRotationSet_MovesTheBlockSoItIsMetAtTheBasePoint_AsAutoCadDoes(string side)
        {
            // AutoCAD 2021: BlockRotation set to 30 degrees moved the box to (105.49, 48.17), still met at (90, 55) on its left,
            // the right leader landed anew from (132.64, 55) (b-k-blockrot30-left and -right).
            DxfMLeader mleader = Of(Drawing(), "BOX", Points(side));

            mleader.BlockRotation = Math.PI / 6;

            Near(P(105.49038105676658, 48.169872981077802), mleader.BlockPosition);
            Near(Math.PI / 6, mleader.BlockRotation);
            Near(side == "left" ? P(82, 55) : P(132.64101615137753, 55), mleader.GetLastVertex(0));
        }

        [Fact]
        public void TheBlockScaleSetAfterTheScale_KeepsTheBlockMetAtTheBasePointItHad_AsAutoCadDoes()
        {
            // AutoCAD 2021: the box met on both sides, at a scale of 2, kept its place (100, 50) and its base point (90, 55); its
            // BlockScale then set to 3 moved it to (150, 25), met at that base point, the leaders from (74, 55) and (286, 55)
            // (k-both-scale2-blockscale3); BlockRotation 30 degrees instead, to (120.98, 41.34), from (74, 55) and (175.28, 55)
            // (k-both-scale2-blockrot30).
            DxfMLeader scaled = Of(Drawing(), "BOX", Points("both"));
            scaled.Scale = 2;
            scaled.BlockScale = new GeoVector3(3, 3, 3);
            Near(P(150, 25), scaled.BlockPosition);
            Near(P(74, 55), scaled.GetLastVertex(0));
            Near(P(286, 55), scaled.GetLastVertex(1));

            DxfMLeader turned = Of(Drawing(), "BOX", Points("both"));
            turned.Scale = 2;
            turned.BlockRotation = Math.PI / 6;
            Near(P(120.98076211353315, 41.339745962155611), turned.BlockPosition);
            Near(P(74, 55), turned.GetLastVertex(0));
            Near(P(175.28203230275508, 55), turned.GetLastVertex(1));
        }

        [Fact]
        public void TheBlockScaleAndRotationOfOtherBlocks_MoveThemAsAutoCadDoes()
        {
            // AutoCAD 2021: connected at its base, BlockScale 2 moved the box to (110, 50), its leader from (82, 50)
            // (k-base-blockscale2-left), and BlockRotation 30 degrees met on the right to (101.55, 50), from (119.55, 50)
            // (k-base-blockrot30-right); the box of origin (5, 5) to (115, 50), from (77, 50) (k-ORIGIN-blockscale2-left); the
            // circle and line of TAG turned 30 degrees met on the right to (99.68, 47.5), from (117.36, 50) (k-TAG-blockrot30-right).
            DxfMLeader based = Of(Drawing(), "BOX", Points("left"), m =>
                m.BlockConnectionType = DxfBlockConnectionType.ConnectBase
            );
            based.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(110, 50), based.BlockPosition);
            Near(P(82, 50), based.GetLastVertex(0));

            DxfMLeader basedTurned = Of(Drawing(), "BOX", Points("right"), m =>
                m.BlockConnectionType = DxfBlockConnectionType.ConnectBase
            );
            basedTurned.BlockRotation = Math.PI / 6;
            Near(P(101.54700538379251, 50), basedTurned.BlockPosition);
            Near(P(119.54700538379251, 50), basedTurned.GetLastVertex(0));

            DxfMLeader origin = Of(Drawing(), "ORIGIN", Points("left"));
            origin.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(115, 50), origin.BlockPosition);
            Near(P(77, 50), origin.GetLastVertex(0));

            DxfMLeader tag = Of(Drawing(), "TAG", Points("right"));
            tag.BlockRotation = Math.PI / 6;
            Near(P(99.682458365518542, 47.5), tag.BlockPosition);
            Near(P(117.36491673103708, 50), tag.GetLastVertex(0));
        }

        [Fact]
        public void TheBlockScaleSetBeforeTheLeaders_MovesTheBlock_AndTheLeaderLandsAtItThere_AsAutoCadDoes()
        {
            // AutoCAD 2021: BlockScale 2 set after the box was placed at (100, 50), before its leader, moved it to (110, 45), and
            // the leader from (0, 0) landed from (82, 55) (k-placed-blockscale2-left); BlockRotation 30 degrees so, to
            // (105.49, 48.17) (k-placed-blockrot30-left).
            DxfMLeader scaled = Of(Drawing(), "BOX", Points("left"), placed: m =>
                m.BlockScale = new GeoVector3(2, 2, 2)
            );
            Near(P(110, 45), scaled.BlockPosition);
            Near(P(82, 55), scaled.GetLastVertex(0));

            DxfMLeader turned = Of(Drawing(), "BOX", Points("left"), placed: m => m.BlockRotation = Math.PI / 6);
            Near(P(105.49038105676658, 48.169872981077802), turned.BlockPosition);
            Near(P(82, 55), turned.GetLastVertex(0));
        }

        [Fact]
        public void TheBlockScaleSet_SnapsALeaderOffItsLandingBack_AsAutoCadDoes()
        {
            // AutoCAD 2021: of two leaders on the left of the box at (160, 40), the second moved off to (40, 70), BlockScale 2
            // moved the box to (170, 35) and landed both from (142, 45) (k-two-unlanded-blockscale2).
            DxfMLeader mleader = AutoCads(Drawing(), BlockTwoUnlanded);

            mleader.BlockScale = new GeoVector3(2, 2, 2);

            Near(P(170, 35), mleader.BlockPosition);
            Near(P(142, 45), mleader.GetLastVertex(0));
            Near(P(142, 45), mleader.GetLastVertex(1));
        }

        // ---- the block's place set (item 2) ------------------------------------------------------------------------------------

        [Theory]
        [InlineData("left")]
        [InlineData("right")]
        [InlineData("both")]
        [InlineData("none")]
        public void TheBlockPositionSet_MovesTheDoglegsAndTheBasePointWithTheBlock_AsAutoCadDoes(string side)
        {
            // AutoCAD 2021: the box moved from (100, 50) to (130, 60) moved the base point from (90, 55) to (120, 65) and the
            // doglegs by as much: the left from (82, 55) to (112, 65), the right from (128, 55) to (158, 65) (k5-BOX-moved-left,
            // k-moved-right, k-moved-both, k-moved-noleader). The library left them.
            DxfMLeader mleader = Of(Drawing(), "BOX", Points(side));

            mleader.BlockPosition = P(130, 60);

            Near(P(120, 65), BasePoint(mleader));
            int line = 0;

            if (side == "left" || side == "both")
            {
                Near(P(112, 65), mleader.GetLastVertex(line++));
            }

            if (side == "right" || side == "both")
            {
                Near(P(158, 65), mleader.GetLastVertex(line));
            }
        }

        [Fact]
        public void TheBlockMovedThenScaled_IsLandedFromTheMovedDogleg_AsAutoCadDoes()
        {
            // AutoCAD 2021: the box moved to (130, 60), then the multileader scaled by 2, stood at (148, 55), met from (112, 65)
            // (k5-BOX-moved-scale2-left); its BlockScale set to 2 instead, at (140, 55) (k-moved-blockscale2-left); met on both
            // sides and scaled by 2 first, then moved, its doglegs from (94, 70) and (186, 70) (k-both-scale2-moved).
            DxfMLeader scaled = Of(Drawing(), "BOX", Points("left"));
            scaled.BlockPosition = P(130, 60);
            scaled.Scale = 2;
            Near(P(148, 55), scaled.BlockPosition);
            Near(P(112, 65), scaled.GetLastVertex(0));

            DxfMLeader blockScaled = Of(Drawing(), "BOX", Points("left"));
            blockScaled.BlockPosition = P(130, 60);
            blockScaled.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(140, 55), blockScaled.BlockPosition);
            Near(P(112, 65), blockScaled.GetLastVertex(0));

            DxfMLeader both = Of(Drawing(), "BOX", Points("both"));
            both.Scale = 2;
            both.BlockPosition = P(130, 60);
            Near(P(94, 70), both.GetLastVertex(0));
            Near(P(186, 70), both.GetLastVertex(1));
        }

        // ---- a multileader of a block transformed (item 3) ---------------------------------------------------------------------

        [Fact]
        public void TransformBy_LeavesTheMultileadersOwnBlockScale_AndWritesTheMatrixAsTheBlocksTransform_AsAutoCadDoes()
        {
            // AutoCAD 2021: TransformBy a scaling by 2 drew the box at (200, 100) twice the size, its BlockScale still read 1 (10
            // left as it was) and its matrix written as the box's transform there (s2-k-transform2); a box of BlockScale 2 turned
            // 30 degrees read 2 and 30 degrees after it (s2-k-turned-transform2). The library wrote 10 as drawn and read 2 and 4.
            DxfMLeader mleader = Of(Drawing(), "BOX", Points("left"));

            mleader.TransformBy(GeoTransform3.Scaling(2.0));

            Near(P(200, 100), mleader.BlockPosition);
            Near(1, mleader.BlockScale.X);
            NearAll(new[] { 2.0, 0, 0, 200, 0, 2, 0, 100 }, Matrix(mleader));
            Near(P(164, 110), mleader.GetLastVertex(0));

            DxfMLeader turned = Of(Drawing(), "BOX", Points("left"), m =>
            {
                m.BlockScale = new GeoVector3(2, 2, 2);
                m.BlockRotation = Math.PI / 6;
            });

            turned.TransformBy(GeoTransform3.Scaling(2.0));

            Near(2, turned.BlockScale.X);
            Near(Math.PI / 6, turned.BlockRotation);
            Near(P(122.03847577293368, 127.32050807568878), turned.GetLastVertex(0));
        }

        [Fact]
        public void TransformBy_LeavesTheMultileadersOwnBlockRotation_AsAutoCadReadsIt()
        {
            // AutoCAD 2021: TransformBy a quarter turn about the origin drew the box turned a quarter at (-50, 100), its
            // BlockRotation still read nought (43 left), its matrix its transform there (k-transform-rot90); turned 30 degrees
            // first, it read 30 degrees (k-blockrot30-transform-rot90). A BlockScale of 3 set after a scaling by 2 put it at
            // (210, 95) (k-transform2-blockscale3).
            DxfMLeader mleader = Of(Drawing(), "BOX", Points("left"));
            mleader.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));
            Near(P(-50, 100), mleader.BlockPosition);
            Near(0, mleader.BlockRotation);
            Near(Math.PI / 2, mleader.Explode().OfType<DxfBlockReference>().Single().Rotation);
            double[] matrix = Matrix(mleader);
            Near(0, matrix[0]);
            Near(-1, matrix[1]);
            Near(-50, matrix[3]);
            Near(1, matrix[4]);
            Near(100, matrix[7]);

            DxfMLeader turned = Of(Drawing(), "BOX", Points("left"));
            turned.BlockRotation = Math.PI / 6;
            turned.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));
            Near(Math.PI / 6, turned.BlockRotation);

            DxfMLeader scaled = Of(Drawing(), "BOX", Points("left"));
            scaled.TransformBy(GeoTransform3.Scaling(2.0));
            scaled.BlockScale = new GeoVector3(3, 3, 3);
            Near(P(210, 95), scaled.BlockPosition);
            Near(3, scaled.BlockScale.X);
        }

        [Fact]
        public void TransformByAMirror_DrawsTheBlockMirrored_AsAutoCadDrawsIt()
        {
            // AutoCAD 2021: mirrored across the y axis, the box at (100, 50) stood at (-100, 50), its normal turned over, its scales
            // 1, its box from (-120, 45) to (-90, 65) (k-transform-mirror); its BlockScale then set to 2 put it at (-110, 45)
            // (k-mirror-blockscale2). The library drew it turned half round, from (-120, 35) to (-90, 55). AutoCAD's mirror about
            // the y axis (Matrix3d.Mirroring of a line) is a half turn about it, as here (D31e); a mirror across the YZ plane keeps
            // the normal and turns the x scale negative (DxfMLeaderD31eTests).
            DxfMLeader mleader = Of(Drawing(), "BOX", Points("left"));

            mleader.TransformBy(GeoTransform3.RotationY(Math.PI));

            Near(P(-100, 50), mleader.BlockPosition);
            Near(1, Number(After(mleader, "296", 16)));
            Near(1, Number(After(mleader, "296", 26)));
            GeoTransform3 drawn = mleader.Explode().OfType<DxfBlockReference>().Single().BlockTransform;
            Near(P(-90, 45), drawn.Transform(P(-10, -5)));
            Near(P(-120, 65), drawn.Transform(P(20, 15)));

            mleader.BlockScale = new GeoVector3(2, 2, 2);
            Near(P(-110, 45), mleader.BlockPosition);
            drawn = mleader.Explode().OfType<DxfBlockReference>().Single().BlockTransform;
            Near(P(-90, 35), drawn.Transform(P(-10, -5)));
            Near(P(-150, 75), drawn.Transform(P(20, 15)));
        }

        // ---- words given with their text style (item 4) ------------------------------------------------------------------------

        [Fact]
        public void WordsOfATextStyle_GiveItToTheMultileader_WithItsBit_AsAutoCadDoes()
        {
            // AutoCAD 2021: words of TS2 given to a multileader of Standard's words wrote TS2 in its 343 and the words' 340 and set
            // bit 11 of its 90 (t-mtext-ts2); given again as read, nothing changed (t-mtext-ts2-again). The library kept Standard.
            DxfDatabase db = Drawing();
            var record = new DxfTextStyleTableRecord { Name = "TS2" };
            db.TextStyleTable.Add(record);
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            Assert.Equal(0, Flags(mleader) & (1 << 11));

            mleader.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
                TextStyleName = "TS2",
            };

            Assert.Equal(record.Handle, After(mleader, "301", 343));
            Assert.Equal(record.Handle, After(mleader, "CONTEXT_DATA{", 340));
            Assert.NotEqual(0, Flags(mleader) & (1 << 11));
            Assert.Equal("TS2", mleader.MText.TextStyleName);

            mleader.MText = mleader.MText;
            Assert.Equal(record.Handle, After(mleader, "301", 343));
        }

        [Fact]
        public void WordsOfStandard_OnAMultileaderOfAnotherTextStyle_MakeItOfStandard_AsAutoCadDoes()
        {
            // AutoCAD 2021: words of Standard given to a multileader whose style's words are of TS2 wrote Standard in its 343 and
            // set bit 11 (t-ts2-current); words of TS2 set nothing (t-ts2-words-ts2).
            DxfDatabase db = Drawing();
            var record = new DxfTextStyleTableRecord { Name = "TS2" };
            db.TextStyleTable.Add(record);
            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");
            List<DxfTypedValue> tail = standard.Item.File.TailOf(standard.Item, 0);
            standard.Item.File.ReplaceTail(
                standard.Item,
                0,
                tail.Select(g => new KeyValuePair<int, string>(g.TypeCode, g.TypeCode == 342 ? record.Handle : g.Value))
                    .ToList()
            );

            var taking = new DxfMLeader();
            db.ModelSpace.AppendEntity(taking);
            Assert.Equal(record.Handle, After(taking, "301", 343));
            taking.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            Assert.Equal(db.TextStyleTable["Standard"].Handle, After(taking, "301", 343));
            Assert.Equal(db.TextStyleTable["Standard"].Handle, After(taking, "CONTEXT_DATA{", 340));
            Assert.NotEqual(0, Flags(taking) & (1 << 11));

            var keeping = new DxfMLeader();
            db.ModelSpace.AppendEntity(keeping);
            keeping.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
                TextStyleName = "TS2",
            };
            Assert.Equal(record.Handle, After(keeping, "301", 343));
            Assert.Equal(0, Flags(keeping) & (1 << 11));
        }

        // ---- leaders off their landing snapped back (item 5) -------------------------------------------------------------------

        [Theory]
        [InlineData("SetDoglegLength0", 143.0, 150.0, 160.0, 40.0)]
        [InlineData("SetDoglegLength1", 150.0, 143.0, 160.0, 40.0)]
        [InlineData("DoglegLength", 143.0, 143.0, 160.0, 40.0)]
        [InlineData("SetDoglegLengthSame", 150.0, 150.0, 160.0, 40.0)]
        [InlineData("DoglegLengthSame", 150.0, 150.0, 160.0, 40.0)]
        [InlineData("LandingGap", 150.0, 150.0, 163.0, 40.0)]
        [InlineData("TextHeight", 150.0, 150.0, 160.0, 43.0)]
        [InlineData("Scale", 142.0, 142.0, 162.0, 42.0)]
        public void ALeaderOffItsLanding_IsSnappedBack_WhenTheWordsAreLaidOutAgain_AsAutoCadDoes(
            string change,
            double first,
            double second,
            double x,
            double y
        )
        {
            // AutoCAD 2021: of two leaders on the left of "ABC" at (160, 40), the first landed from (150, 38), the second moved
            // off to (40, 70): a dogleg of 15 given to the first started it at (143, 38) and landed the second from (150, 38)
            // (d6-two-setdl0); given to the second, (143, 38) (d6-two-setdl1); to both, (143, 38) (d-two-dl15); the length they
            // have, both from (150, 38) (d-two-setdl0-same, d-two-dl-same); a gap of 5 moved the words to (163, 40)
            // (d-two-gap5); a height of 10 to (160, 43) (two-unlanded-height10); a scale of 2 to (162, 42), both from (142, 38)
            // (d-two-scale2). The library left the second at (40, 70) and moved the words of the scale to (170, 42).
            DxfMLeader mleader = AutoCads(Drawing(), TwoUnlanded);

            switch (change)
            {
                case "SetDoglegLength0":
                    mleader.SetDoglegLength(0, 15);
                    break;
                case "SetDoglegLength1":
                    mleader.SetDoglegLength(1, 15);
                    break;
                case "DoglegLength":
                    mleader.DoglegLength = 15;
                    break;
                case "SetDoglegLengthSame":
                    mleader.SetDoglegLength(0, 8);
                    break;
                case "DoglegLengthSame":
                    mleader.DoglegLength = 8;
                    break;
                case "LandingGap":
                    mleader.LandingGap = 5;
                    break;
                case "TextHeight":
                    mleader.TextHeight = 10;
                    break;
                default:
                    mleader.Scale = 2;
                    break;
            }

            Near(P(first, 38), mleader.GetLastVertex(0));
            Near(P(second, 38), mleader.GetLastVertex(1));
            Near(P(x, y), mleader.TextLocation);
        }

        [Theory]
        [InlineData("SetDoglegLength0", 135.0, 142.0, 45.0)]
        [InlineData("DoglegLength", 135.0, 135.0, 45.0)]
        [InlineData("Scale", 124.0, 124.0, 50.0)]
        public void ALeaderOffItsLandingOnABlock_IsSnappedBack_AsAutoCadDoes(
            string change,
            double first,
            double second,
            double y
        )
        {
            // AutoCAD 2021: of two leaders on the left of the box at (160, 40), the second moved off to (40, 70): a dogleg of 15
            // given to the first landed it from (135, 45) and the second from (142, 45) (d6-k-two-setdl0); to both, from (135, 45)
            // (d-k-two-dl15); a scale of 2 kept the box and landed both from (124, 50) (e-k-twoleft-unlanded-scale2).
            DxfMLeader mleader = AutoCads(Drawing(), BlockTwoUnlanded);

            switch (change)
            {
                case "SetDoglegLength0":
                    mleader.SetDoglegLength(0, 15);
                    break;
                case "DoglegLength":
                    mleader.DoglegLength = 15;
                    break;
                default:
                    mleader.Scale = 2;
                    break;
            }

            Near(P(160, 40), mleader.BlockPosition);
            Near(P(first, y), mleader.GetLastVertex(0));
            Near(P(second, y), mleader.GetLastVertex(1));
        }

        [Fact]
        public void ALeaderOffItsLandingOnTheLeft_IsSnappedBack_WithALeaderOnTheRight_AsAutoCadDoes()
        {
            // AutoCAD 2021: "ABC" at (-10, 40) met on the right from (10.67, 38), the leader on the left moved off to (160, 70) and
            // given a dogleg of 15, landed from (-27, 38), the right kept (d-tworight-setdl1).
            DxfMLeader mleader = AutoCads(Drawing(), RightAndUnlanded);

            mleader.SetDoglegLength(1, 15);

            Near(P(10.66666666666667, 38), mleader.GetLastVertex(0));
            Near(P(-27, 38), mleader.GetLastVertex(1));
            Near(P(-10, 40), mleader.TextLocation);
        }

        // ---- several leaders on one side under the scale (item 6) --------------------------------------------------------------

        [Fact]
        public void WordsMetByTwoLeadersOnTheirLeft_MoveByTheGapUnderTheScale_AndTheLeadersLandAnew_AsAutoCadDoes()
        {
            // AutoCAD 2021: "ABC" at (160, 40) met from (150, 38) twice, at a scale of 2, stood at (162, 42), both doglegs from
            // (142, 38), the base point kept at (158, 38) (e-words-twoleft-scale2). The library moved them by the doglegs too, to
            // (170, 42).
            DxfMLeader mleader = AutoCads(Drawing(), TwoLanded);

            mleader.Scale = 2;

            Near(P(162, 42), mleader.TextLocation);
            Near(P(142, 38), mleader.GetLastVertex(0));
            Near(P(142, 38), mleader.GetLastVertex(1));
            Near(P(158, 38), BasePoint(mleader));
        }

        [Fact]
        public void ABlockMetByTwoLeadersOnASide_StaysUnderTheScale_AndTheLeadersLandAnew_AsAutoCadDoes()
        {
            // AutoCAD 2021: the box at (160, 40) met from (142, 45) twice on its left stayed at a scale of 2, both from (124, 50)
            // (e-k-twoleft-scale2); at (172, 65) met from (200, 70) twice on its right, both from (228, 75) (e-k-tworight-scale2).
            // D31c's guess, now checked.
            DxfMLeader left = AutoCads(Drawing(), BlockTwoLanded);
            left.Scale = 2;
            Near(P(160, 40), left.BlockPosition);
            Near(P(124, 50), left.GetLastVertex(0));
            Near(P(124, 50), left.GetLastVertex(1));

            DxfMLeader right = AutoCads(Drawing(), BlockTwoRight);
            right.Scale = 2;
            Near(P(172, 65), right.BlockPosition);
            Near(P(228, 75), right.GetLastVertex(0));
            Near(P(228, 75), right.GetLastVertex(1));
        }
    }
}
