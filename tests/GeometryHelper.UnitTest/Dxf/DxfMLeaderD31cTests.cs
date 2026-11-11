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
    /// The next leftovers of a MULTILEADER as AutoCAD 2021 makes and edits it (plugin D31c, rounds A and B, on a Standard of
    /// words 4 high, arrows 4, a gap of 2 and doglegs of 8): the attachment property of the left side, the sizes read and
    /// written unscaled, the text style of the words, the bit of the overrides of a value set again, a block scaled with the
    /// multileader, a style of no arrow, and one leader of several on a side given its own dogleg.
    /// </summary>
    public class DxfMLeaderD31cTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        // AutoCAD 2021's records (after AcDbMLeader, its proxy graphics left out) of words "ABC" 4 high held at (100, 50): a line
        // from (250, 0) and its leader on the right, its dogleg of 8 from (120.67, 48) (D31's d02) ...
        private const string Right =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... a line from (0, 0) on the left too, its dogleg from (90, 48) (d03) ...
        private const string Both =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... and the words moved to (160, 40), met on the left by two leaders of their own, both landed at (150, 38) (D31c b-two-landed).
        private const string TwoLeft =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 158.0|20 38.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 160.0|22 40.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 150.0|20 38.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 150.0|20 38.0|30 0.0|11 1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 296064|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

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

        /// <summary>A multileader made in code as GHD31C made AutoCAD's: words 4 high held at (100, 50), lines from the points given (from (0, 0) where none is).</summary>
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
        /// A block of GHD31C's: BOX the lines of the box from (-10, -5) to (20, 15); TAG a circle of radius 10 about the origin and a
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

        /// <summary>A multileader of a block of GHD31C's placed at (100, 50), as GHD31C made AutoCAD's, before its leaders.</summary>
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

        /// <summary>The record of an entity as "code value|...", for comparing one before and after.</summary>
        private static string Record(DxfEntity entity) =>
            string.Join(
                "|",
                entity
                    .Item.File.TailOf(entity.Item, 0)
                    .Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim())
            );

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static int Flags(DxfMLeader mleader) =>
            int.Parse(After(mleader, "301", 90), CultureInfo.InvariantCulture);

        /// <summary>
        /// Writes a group of the multileader's own (after its context data, 301) as text: the first of its code there, or, where it
        /// has none, one put in before its group of a code given.
        /// </summary>
        private static void SetOwn(DxfMLeader mleader, int code, string value, int before = 42)
        {
            List<DxfTypedValue> tail = mleader.Item.File.TailOf(mleader.Item, 0);
            int close = tail.FindIndex(g => g.TypeCode == 301);
            var groups = tail.Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            int at = groups.FindIndex(close, g => g.Key == code);

            if (at >= 0)
            {
                groups[at] = new KeyValuePair<int, string>(code, value);
            }
            else
            {
                groups.Insert(
                    groups.FindIndex(close, g => g.Key == before),
                    new KeyValuePair<int, string>(code, value)
                );
            }

            mleader.Item.File.ReplaceTail(mleader.Item, 0, groups);
        }

        /// <summary>The drawing with a text style TS2, which its multileader style Standard names for its words (342), as GHD31C's S4.</summary>
        private static DxfDatabase WithTextStyle(out string ts2)
        {
            DxfDatabase db = Drawing();
            var record = new DxfTextStyleTableRecord { Name = "TS2" };
            db.TextStyleTable.Add(record);
            string handle = record.Handle;
            ts2 = handle;
            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");
            List<DxfTypedValue> tail = standard.Item.File.TailOf(standard.Item, 0);
            var groups = tail.Select(g => new KeyValuePair<int, string>(
                    g.TypeCode,
                    g.TypeCode == 342 ? handle : g.Value
                ))
                .ToList();
            standard.Item.File.ReplaceTail(standard.Item, 0, groups);
            Assert.Equal("TS2", standard.TextStyle.Name);
            return db;
        }

        // ---- the attachment property (item 1) ------------------------------------------------------------------------------

        [Fact]
        public void TheAttachmentProperty_SetsTheLeftSideAlone_AsAutoCadSetsIt()
        {
            // AutoCAD 2021: TextAttachmentType set to the bottom of the bottom line on three lines met on both sides did what
            // SetTextAttachmentType of the left alone does, the right kept at the middle of the top line (m2-both-att4 and
            // m2-both-attL4 alike): the words at (100, 65.33), the right dogleg from (120.67, 63.33), 173 and 174 of 4, 95 and
            // 175 of 1. The library set both sides.
            DxfMLeader mleader = AutoCads(Drawing(), Both);
            mleader.MText = new DxfMText
            {
                Contents = Three,
                Location = P(100, 50),
                TextHeight = 4.0,
            };

            mleader.TextAttachmentType = DxfTextAttachmentType.AttachmentBottomOfBottom;

            Assert.Equal(
                DxfTextAttachmentType.AttachmentBottomOfBottom,
                mleader.GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader)
            );
            Assert.Equal(
                DxfTextAttachmentType.AttachmentMiddleOfTop,
                mleader.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader)
            );
            Assert.Equal("1", After(mleader, "301", 95));
            Assert.Equal("1", After(mleader, "CONTEXT_DATA{", 175));
            Near(P(100, 65.333333333333329), mleader.TextLocation);
            Near(P(90, 48), mleader.GetLastVertex(0));
            Near(P(120.66666666666666, 63.333333333333329), mleader.GetLastVertex(1));
        }

        [Fact]
        public void TheAttachmentProperty_LeavesWordsMetOnTheRightAlone_AsAutoCadLeavesThem()
        {
            // AutoCAD 2021 (att-prop8-right): attachment 8 by the property on words met on the right alone wrote 173 and 174 of 8,
            // left 95 at 1 and the words "ABC" where they were, not underlined. The library underlined them.
            DxfMLeader mleader = AutoCads(Drawing(), Right);

            mleader.TextAttachmentType = DxfTextAttachmentType.AttachmentAllLine;

            Assert.Equal("8", After(mleader, "301", 173));
            Assert.Equal("1", After(mleader, "301", 95));
            Assert.Equal("ABC", After(mleader, "CONTEXT_DATA{", 304));
            Near(P(100, 50), mleader.TextLocation);
            Near(P(120.66666666666666, 48), mleader.GetLastVertex(0));
        }

        // ---- the sizes read and written unscaled (item 2) ------------------------------------------------------------------

        [Fact]
        public void TheGapAndHeightOfAScaledMultileader_AreReadAndWrittenUnscaled_AsAutoCadDoes()
        {
            // AutoCAD 2021 at a scale of 2: LandingGap read 2 and TextHeight 4 (a-scale2); a gap of 3 wrote 145 of 6 and moved the
            // words from (110, 52) to (112, 52) (s2-gap3); a height of 5 wrote 41 of 10 and moved them to (110, 53)
            // (m2-left-scale2-height5). The library read and wrote them as drawn.
            DxfMLeader scaled = Words(Drawing());
            scaled.Scale = 2;
            Near(2.0, scaled.LandingGap);
            Near(4.0, scaled.TextHeight);

            DxfMLeader gap = Words(Drawing());
            gap.Scale = 2;
            gap.LandingGap = 3;
            Near(3.0, gap.LandingGap);
            Assert.Equal(6.0, Number(After(gap, "CONTEXT_DATA{", 145)));
            Near(P(112, 52), gap.TextLocation);

            DxfMLeader high = Words(Drawing());
            high.Scale = 2;
            high.TextHeight = 5;
            Near(5.0, high.TextHeight);
            Assert.Equal(10.0, Number(After(high, "CONTEXT_DATA{", 41)));
            Near(P(110, 53), high.TextLocation);
            Assert.Equal(10.0, high.MText.TextHeight);
        }

        [Fact]
        public void EverySizeOfAScaledMultileader_IsReadAsItWasSet_AsAutoCadReadsIt()
        {
            // AutoCAD 2021 (s2-five): at a scale of 2, a height of 5, a gap of 3, arrows of 6 and doglegs of 7 read so again, written
            // 41 of 10, 145 of 6, 140 of 12 and 42 of 6, the MULTILEADER's 41 of 7 and the leader's 40 of 14; the words at (110, 53).
            DxfMLeader mleader = Words(Drawing());
            mleader.Scale = 2;
            mleader.TextHeight = 5;
            mleader.LandingGap = 3;
            mleader.ArrowSize = 6;
            mleader.DoglegLength = 7;

            Near(5.0, mleader.TextHeight);
            Near(3.0, mleader.LandingGap);
            Near(6.0, mleader.ArrowSize);
            Near(7.0, mleader.DoglegLength);
            Assert.Equal(10.0, Number(After(mleader, "CONTEXT_DATA{", 41)));
            Assert.Equal(6.0, Number(After(mleader, "CONTEXT_DATA{", 145)));
            Assert.Equal(12.0, Number(After(mleader, "CONTEXT_DATA{", 140)));
            Assert.Equal(14.0, Number(After(mleader, "LEADER{", 40)));
            Assert.Equal(7.0, Number(After(mleader, "301", 41)));
            Assert.Equal(6.0, Number(After(mleader, "301", 42)));
            Near(P(110, 53), mleader.TextLocation);
        }

        [Fact]
        public void TheArrowsAndDoglegsOfAMultileaderScaledByTransformBy_AreReadAsDrawn_AsAutoCadReadsThem()
        {
            // AutoCAD 2021 (s2-arrow-read-transform): TransformBy by 2 left the scale at 1 and 42 and 41 at 4 and 8, and read
            // ArrowSize 8, DoglegLength 16, LandingGap 4 and TextHeight 8, a line's GetArrowSize still 4; Scale 2 then read them
            // the same (s2-transform-scale2). The library read 4 and 8 from 42 and 41.
            DxfMLeader mleader = Words(Drawing());
            mleader.TransformBy(GeoTransform3.Scaling(2.0));

            Near(8.0, mleader.ArrowSize);
            Near(16.0, mleader.DoglegLength);
            Near(4.0, mleader.LandingGap);
            Near(8.0, mleader.TextHeight);
            Near(4.0, mleader.GetArrowSize(0));

            mleader.Scale = 2;
            Near(8.0, mleader.ArrowSize);
            Near(16.0, mleader.DoglegLength);
            Near(4.0, mleader.LandingGap);
            Near(8.0, mleader.TextHeight);
        }

        [Fact]
        public void TheDoglegLength_IsReadFromTheFirstLeader_ElseFromTheMultileader_AsAutoCadReadsIt()
        {
            // AutoCAD 2021: SetDoglegLength(0, 15) read DoglegLength 15, the MULTILEADER's 41 still 8 (m-left-setdl-after); of two
            // leaders, the second given 15 read 8 (s2-two-setdl1); with no leader, TransformBy by 2 read the MULTILEADER's 8
            // (s2-noleader-transform2).
            DxfMLeader first = Words(Drawing());
            first.SetDoglegLength(0, 15);
            Near(15.0, first.DoglegLength);
            Assert.Equal(8.0, Number(After(first, "301", 41)));

            DxfMLeader second = AutoCads(Drawing(), TwoLeft);
            second.SetDoglegLength(1, 15);
            Near(8.0, second.DoglegLength);

            DxfMLeader none = Words(Drawing());
            none.RemoveLeader(none.GetLeaderIndexes()[0]);
            none.TransformBy(GeoTransform3.Scaling(2.0));
            Near(8.0, none.DoglegLength);
        }

        [Fact]
        public void TheBlockScaleOfAScaledMultileader_IsReadAndWrittenUnscaled_AsAutoCadDoes()
        {
            // AutoCAD 2021: Scale 2 on BOX read BlockScale 1, drawn 2 (16 of 2, k2-BOX-scale2-after); BlockScale 3 then wrote the
            // MULTILEADER's 10 of 3 and 16 of 6 (s2-k-scale2-blockscale3); a block given after Scale 2 was drawn 2 and met from
            // (64, 60) (k5-BOX-scale2-before-left). The library read 16 and wrote the scale given in both.
            DxfMLeader scaled = Of(Drawing(), "BOX");
            scaled.AddLeaderLine(P(0, 0));
            scaled.Scale = 2;
            Near(P(1, 1, 1), Pt(scaled.BlockScale));
            Assert.Equal(2.0, Number(After(scaled, "CONTEXT_DATA{", 16)));

            scaled.BlockScale = new GeoVector3(3, 3, 3);
            Near(P(3, 3, 3), Pt(scaled.BlockScale));
            Assert.Equal(6.0, Number(After(scaled, "CONTEXT_DATA{", 16)));
            Assert.Equal(3.0, Number(After(scaled, "301", 10)));

            DxfDatabase db = Drawing();
            var before = new DxfMLeader();
            db.ModelSpace.AppendEntity(before);
            before.Scale = 2;
            before.BlockContent = Block(db, "BOX");
            before.BlockPosition = P(100, 50);
            before.AddLeaderLine(P(0, 0));
            Assert.Equal(2.0, Number(After(before, "CONTEXT_DATA{", 16)));
            Near(P(1, 1, 1), Pt(before.BlockScale));
            Near(P(64, 60), before.GetLastVertex(0));
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint3 Pt(GeoVector3 v) => new GeoPoint3(v.X, v.Y, v.Z);

        // ---- the text style of the words (item 3) -------------------------------------------------------------------------

        [Fact]
        public void ANewMultileader_WritesItsTextStyleInItsWords_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 (t-standard): a new multileader of words wrote the handle of the text style Standard in its 343 and
            // in its words' 340. The library wrote the words' 340 as 0.
            DxfDatabase db = Drawing();
            DxfMLeader mleader = Words(db);

            string standard = db.TextStyleTable["Standard"].Handle;
            Assert.Equal(standard, After(mleader, "301", 343));
            Assert.Equal(standard, After(mleader, "CONTEXT_DATA{", 340));
        }

        [Fact]
        public void ANewMultileaderOfAStyleOfATextStyle_TakesThatTextStyle_AsAutoCadTakesIt()
        {
            // AutoCAD 2021: a new multileader of a style whose words are of the text style TS2 wrote TS2 in its 343
            // (t-ts2-current-block), and set to that style, in its 343 and its words' 340 (t-ts2-set-after). The library wrote
            // Standard.
            DxfDatabase db = WithTextStyle(out string ts2);
            DxfMLeader words = Words(db);
            Assert.Equal(ts2, After(words, "301", 343));
            Assert.Equal(ts2, After(words, "CONTEXT_DATA{", 340));

            DxfMLeader block = Of(db, "BOX");
            Assert.Equal(ts2, After(block, "301", 343));
        }

        [Fact]
        public void TheStyleSet_GivesTheWordsItsTextStyle_UnlessTheMultileaderHasItsOwn_AsAutoCadDoes()
        {
            // AutoCAD 2021: a multileader set to a style whose words are of TS2 took TS2 in its 343 and its words' 340
            // (t-ts2-set-after); one of a text style of its own (bit 11 of its 90) kept it (b-ts3-then-s4).
            DxfDatabase db = WithTextStyle(out string ts2);
            string standard = db.TextStyleTable["Standard"].Handle;

            DxfMLeader taking = Words(db);
            SetOwn(taking, 340, "FFFF");
            SetOwn(taking, 343, standard);
            taking.MLeaderStyle = "Standard";
            Assert.Equal(ts2, After(taking, "301", 343));
            Assert.Equal(ts2, After(taking, "CONTEXT_DATA{", 340));

            DxfMLeader keeping = Words(db);
            SetOwn(keeping, 340, "FFFF");
            SetOwn(keeping, 343, standard);
            SetOwn(keeping, 90, (Flags(keeping) | (1 << 11)).ToString(CultureInfo.InvariantCulture));
            keeping.MLeaderStyle = "Standard";
            Assert.Equal(standard, After(keeping, "301", 343));
        }

        // ---- the bit of the overrides of a value set again (item 4) -------------------------------------------------------

        [Theory]
        [InlineData("LeaderLineType", 0)]
        [InlineData("LeaderLineColor", 1)]
        [InlineData("LeaderLineWeight", 3)]
        [InlineData("EnableLanding", 4)]
        [InlineData("LandingGap", 5)]
        [InlineData("EnableDogleg", 6)]
        [InlineData("DoglegLength", 7)]
        [InlineData("SetDoglegLength", 7)]
        [InlineData("ArrowSymbol", 8)]
        [InlineData("TextAttachmentType", 12)]
        [InlineData("SetTextAttachmentTypeLeft", 12)]
        [InlineData("TextAngleType", 13)]
        [InlineData("TextAlignmentType", 14)]
        [InlineData("TextHeight", 16)]
        [InlineData("EnableFrameText", 17)]
        [InlineData("Scale", 24)]
        [InlineData("SetTextAttachmentTypeRight", 25)]
        [InlineData("SetTextAttachmentTypeTop", 28)]
        [InlineData("SetTextAttachmentTypeBottom", 29)]
        [InlineData("ExtendLeaderToText", 30)]
        [InlineData("BlockColor", 20)]
        [InlineData("BlockScale", 21)]
        [InlineData("BlockRotation", 22)]
        [InlineData("BlockConnectionType", 23)]
        public void APropertySetToTheValueItHas_SetsItsBitOfTheOverrides_AndWritesNothingElse_AsAutoCadDoes(
            string property,
            int bit
        )
        {
            // AutoCAD 2021 (o-*, ok-*): each of these set to the value it had set its bit of the overrides (90), and nothing
            // else changed. The library changed nothing.
            bool block = bit >= 19 && bit <= 23;
            DxfMLeader mleader = block ? Of(Drawing(), "BOX") : Words(Drawing());

            if (block)
            {
                mleader.AddLeaderLine(P(0, 0));
            }

            int flags = Flags(mleader) & ~(1 << bit);
            SetOwn(mleader, 90, flags.ToString(CultureInfo.InvariantCulture));
            string before = Record(mleader).Replace("90 " + flags.ToString(CultureInfo.InvariantCulture), "90 #");

            switch (property)
            {
                case "LeaderLineType":
                    mleader.LeaderLineType = mleader.LeaderLineType;
                    break;
                case "LeaderLineColor":
                    mleader.LeaderLineColor = mleader.LeaderLineColor;
                    break;
                case "LeaderLineWeight":
                    mleader.LeaderLineWeight = mleader.LeaderLineWeight;
                    break;
                case "EnableLanding":
                    mleader.EnableLanding = mleader.EnableLanding;
                    break;
                case "LandingGap":
                    mleader.LandingGap = mleader.LandingGap;
                    break;
                case "EnableDogleg":
                    mleader.EnableDogleg = mleader.EnableDogleg;
                    break;
                case "DoglegLength":
                    mleader.DoglegLength = mleader.DoglegLength;
                    break;
                case "SetDoglegLength":
                    mleader.SetDoglegLength(0, mleader.GetDoglegLength(0));
                    break;
                case "ArrowSymbol":
                    mleader.ArrowSymbol = mleader.ArrowSymbol;
                    break;
                case "TextAttachmentType":
                    mleader.TextAttachmentType = mleader.TextAttachmentType;
                    break;
                case "SetTextAttachmentTypeLeft":
                    mleader.SetTextAttachmentType(
                        mleader.GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader),
                        DxfLeaderDirectionType.LeftLeader
                    );
                    break;
                case "TextAngleType":
                    mleader.TextAngleType = mleader.TextAngleType;
                    break;
                case "TextAlignmentType":
                    mleader.TextAlignmentType = mleader.TextAlignmentType;
                    break;
                case "TextHeight":
                    mleader.TextHeight = mleader.TextHeight;
                    break;
                case "EnableFrameText":
                    mleader.EnableFrameText = mleader.EnableFrameText;
                    break;
                case "Scale":
                    mleader.Scale = mleader.Scale;
                    break;
                case "SetTextAttachmentTypeRight":
                    mleader.SetTextAttachmentType(
                        mleader.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader),
                        DxfLeaderDirectionType.RightLeader
                    );
                    break;
                case "SetTextAttachmentTypeTop":
                    mleader.SetTextAttachmentType(
                        mleader.GetTextAttachmentType(DxfLeaderDirectionType.TopLeader),
                        DxfLeaderDirectionType.TopLeader
                    );
                    break;
                case "SetTextAttachmentTypeBottom":
                    mleader.SetTextAttachmentType(
                        mleader.GetTextAttachmentType(DxfLeaderDirectionType.BottomLeader),
                        DxfLeaderDirectionType.BottomLeader
                    );
                    break;
                case "ExtendLeaderToText":
                    mleader.ExtendLeaderToText = mleader.ExtendLeaderToText;
                    break;
                case "BlockColor":
                    mleader.BlockColor = mleader.BlockColor;
                    break;
                case "BlockScale":
                    mleader.BlockScale = mleader.BlockScale;
                    break;
                case "BlockRotation":
                    mleader.BlockRotation = mleader.BlockRotation;
                    break;
                case "BlockConnectionType":
                    mleader.BlockConnectionType = mleader.BlockConnectionType;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(property), property, "No such case.");
            }

            // BlockScale and BlockRotation wrote the block's matrix as its transform where it stands too (ok-blockscale, ok-blockrot,
            // read again in D31e).
            if (property == "BlockScale" || property == "BlockRotation")
            {
                before = before.Replace(
                    "|47 1|47 0|47 0|47 0|47 0|47 1|47 0|47 0|",
                    "|47 1|47 0|47 0|47 100|47 0|47 1|47 0|47 50|"
                );
            }

            Assert.Equal(flags | (1 << bit), Flags(mleader));
            Assert.Equal(
                before,
                Record(mleader).Replace("90 " + (flags | (1 << bit)).ToString(CultureInfo.InvariantCulture), "90 #")
            );
        }

        [Fact]
        public void TheDirectionAnnotationScalePlaceAndStyleSetToWhatTheyAre_ChangeNothing_AsAutoCadChangesNothing()
        {
            // AutoCAD 2021: TextAttachmentDirection, EnableAnnotationScale, TextLocation, MLeaderStyle and the words given as
            // they were set no bit (o-dir, o-annot, o-textloc, o-style, o-mtext).
            DxfMLeader mleader = Words(Drawing());
            string before = Record(mleader);

            mleader.TextAttachmentDirection = mleader.TextAttachmentDirection;
            mleader.EnableAnnotationScale = mleader.EnableAnnotationScale;
            mleader.TextLocation = mleader.TextLocation;
            mleader.MLeaderStyle = mleader.MLeaderStyle;
            mleader.MText = mleader.MText;

            Assert.Equal(before, Record(mleader));
        }

        [Fact]
        public void SetDoglegLength_SetsTheBitOfTheDoglegs_AsAutoCadSetsIt()
        {
            // AutoCAD 2021 (s2-setdl0-15, a-m-left-setdl-after): SetDoglegLength(0, 15) set bit 7 of the overrides. The library
            // set none.
            DxfMLeader mleader = Words(Drawing());
            int flags = Flags(mleader) & ~(1 << 7);
            SetOwn(mleader, 90, flags.ToString(CultureInfo.InvariantCulture));

            mleader.SetDoglegLength(0, 15);

            Assert.Equal(flags | (1 << 7), Flags(mleader));
        }

        [Fact]
        public void ALeaderLineGivenTheLookItHas_IsFlaggedAsItsOwn_AsAutoCadFlagsIt()
        {
            // AutoCAD 2021: SetArrowSize(0, 4) on arrows of 4 wrote the line's 40 of 4 and its flags (93) of 16 (o-setarrow0);
            // SetLeaderLineType, SetLeaderLineColor and SetLeaderLineWeight of what the line had flagged it 1, 2 and 8
            // (b-line-type-same, b-line-color-same, b-line-weight-same). The library changed nothing.
            DxfMLeader arrow = Words(Drawing());
            arrow.SetArrowSize(0, 4);
            Assert.Equal("16", After(arrow, "LEADER_LINE{", 93));
            Assert.Equal(4.0, Number(After(arrow, "LEADER_LINE{", 40)));

            DxfMLeader type = Words(Drawing());
            type.SetLeaderLineType(0, type.GetLeaderLineType(0));
            Assert.Equal("1", After(type, "LEADER_LINE{", 93));
            Assert.Equal(DxfLeaderType.StraightLeader, type.GetLeaderLineType(0));

            DxfMLeader colour = Words(Drawing());
            colour.SetLeaderLineColor(0, colour.GetLeaderLineColor(0));
            Assert.Equal("2", After(colour, "LEADER_LINE{", 93));

            DxfMLeader weight = Words(Drawing());
            weight.SetLeaderLineWeight(0, weight.GetLeaderLineWeight(0));
            Assert.Equal("8", After(weight, "LEADER_LINE{", 93));
            Assert.Equal(weight.LeaderLineWeight, weight.GetLeaderLineWeight(0));
        }

        // ---- a block scaled with the multileader (item 5) -----------------------------------------------------------------

        [Theory]
        [InlineData("BOX", 0.0, false, 2.0, 118.0, 45.0, 82.0, 55.0, 98.0, 55.0)]
        [InlineData("BOX", 250.0, false, 2.0, 72.0, 45.0, 128.0, 55.0, 52.0, 55.0)]
        [InlineData("BOX", 0.0, false, 3.0, 136.0, 40.0, 82.0, 55.0, 106.0, 55.0)]
        [InlineData("BOX", 0.0, true, 2.0, 118.0, 50.0, 82.0, 50.0, 98.0, 50.0)]
        [InlineData("TAG", 0.0, false, 2.0, 118.0, 50.0, 82.0, 50.0, 98.0, 50.0)]
        [InlineData("ORIGIN", 0.0, false, 2.0, 123.0, 50.0, 77.0, 50.0, 93.0, 50.0)]
        public void TheScaleOfABlockMetOnOneSide_ScalesTheBlockAndLandsItAtTheDogleg_AsAutoCadDoes(
            string kind,
            double from,
            bool atBase,
            double scale,
            double x,
            double y,
            double lastX,
            double lastY,
            double baseX,
            double baseY
        )
        {
            // AutoCAD 2021 (k2-BOX-scale2-after, k5-*): Scale scaled the block as drawn (16) and its doglegs, kept each dogleg's
            // start, and moved the block so that it meets the longer dogleg's end as a leader added would; the base point of the
            // content (10) went to where a leader from the left meets the block. The library left the block where it was, unscaled.
            DxfMLeader mleader = Of(Drawing(), kind, m =>
            {
                if (atBase)
                {
                    m.BlockConnectionType = DxfBlockConnectionType.ConnectBase;
                }
            });

            mleader.AddLeaderLine(P(from, 0));
            mleader.Scale = scale;

            Near(P(x, y), mleader.BlockPosition);
            Near(P(lastX, lastY), mleader.GetLastVertex(0));
            Assert.Equal(scale, Number(After(mleader, "CONTEXT_DATA{", 16)));
            Near(P(1, 1, 1), Pt(mleader.BlockScale));
            Near(
                P(baseX, baseY),
                new GeoPoint3(
                    Number(After(mleader, "CONTEXT_DATA{", 10)),
                    Number(After(mleader, "CONTEXT_DATA{", 20)),
                    0
                )
            );
            Near(P(x, y), mleader.Explode().OfType<DxfBlockReference>().Single().Position);
        }

        [Fact]
        public void TheScaleOfABlock_WritesItsMatrixAtThePlaceItHadBeforeItMoved_AsAutoCadWritesIt()
        {
            // AutoCAD 2021: Scale 2 on BOX at (100, 50) met on the left wrote the matrix (47) with the scale 2 and the translation
            // (100, 50) though the block moved to (118, 45) (k2-BOX-scale2-after); ORIGIN, its origin at (5, 5), with (90, 40)
            // (k5-ORIGIN-scale2-left).
            DxfMLeader box = Of(Drawing(), "BOX");
            box.AddLeaderLine(P(0, 0));
            box.Scale = 2;
            double[] expected = { 2, 0, 0, 100, 0, 2, 0, 50, 0, 0, 2, 0, 0, 0, 0, 1 };

            for (int i = 0; i < 16; i++)
            {
                Near(expected[i], Number(After(box, "CONTEXT_DATA{", 47, i)));
            }

            DxfMLeader origin = Of(Drawing(), "ORIGIN");
            origin.AddLeaderLine(P(0, 0));
            origin.Scale = 2;
            Near(90.0, Number(After(origin, "CONTEXT_DATA{", 47, 3)));
            Near(40.0, Number(After(origin, "CONTEXT_DATA{", 47, 7)));
        }

        [Fact]
        public void TheScaleOfABlockTurned_LandsItAtTheDoglegOnItsTurnedSide_AsAutoCadDoes()
        {
            // AutoCAD 2021 (k5-BOX-turned-scale2-left): BOX scaled by 2 and turned by 30 degrees, then Scale 2, was drawn 4 at
            // (138.98, 36.34), its dogleg kept from (61.02, 63.66). The scale and turn set before the block, as GHD31C set them:
            // set after it is placed, they move it (D31d, k-placed-blockscale2-left).
            DxfDatabase db = Drawing();
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.BlockScale = new GeoVector3(2, 2, 2);
            mleader.BlockRotation = Math.PI / 6;
            mleader.BlockContent = Block(db, "BOX");
            mleader.BlockPosition = P(100, 50);
            mleader.AddLeaderLine(P(0, 0));
            mleader.Scale = 2;

            Near(P(138.98076211353316, 36.339745962155611), mleader.BlockPosition);
            Near(P(61.01923788646684, 63.660254037844389), mleader.GetLastVertex(0));
            Assert.Equal(4.0, Number(After(mleader, "CONTEXT_DATA{", 16)));
        }

        [Fact]
        public void TheScaleOfABlockMetOnBothSidesOrNone_KeepsThePlaceAndLandsTheLeadersAnew_AsAutoCadDoes()
        {
            // AutoCAD 2021: Scale 2 on BOX met on both sides kept it at (100, 50), drawn 2, its doglegs landed anew from (64, 60)
            // and (156, 60) (k5-BOX-scale2-both); met by none it stayed (k5-BOX-noleader-scale2); 2 then 3 stood at (136, 40) as
            // 3 at once, and 2 then 1 at (100, 50) again (k5-BOX-scale2-then3-left, k5-BOX-scale2-back1-left).
            DxfMLeader both = Of(Drawing(), "BOX");
            both.AddLeaderLine(P(0, 0));
            both.AddLeaderLine(P(250, 0));
            both.Scale = 2;
            Near(P(100, 50), both.BlockPosition);
            Near(P(64, 60), both.GetLastVertex(0));
            Near(P(156, 60), both.GetLastVertex(1));

            DxfMLeader none = Of(Drawing(), "BOX");
            none.Scale = 2;
            Near(P(100, 50), none.BlockPosition);
            Assert.Equal(2.0, Number(After(none, "CONTEXT_DATA{", 16)));

            DxfMLeader twice = Of(Drawing(), "BOX");
            twice.AddLeaderLine(P(0, 0));
            twice.Scale = 2;
            twice.Scale = 3;
            Near(P(136, 40), twice.BlockPosition);
            twice.Scale = 1;
            Near(P(100, 50), twice.BlockPosition);
            Near(P(82, 55), twice.GetLastVertex(0));
        }

        // ---- a style of no arrow, and one leader of several given its dogleg (item 6) -------------------------------------

        [Fact]
        public void TheStyleSetToOneOfNoArrow_TakesTheArrowAway_UnlessTheMultileaderHasItsOwn_AsAutoCadDoes()
        {
            // AutoCAD 2021: a multileader of S2's arrow set to Standard, of no arrow, wrote no 342 (l6-s2-to-standard); one of an
            // arrow of its own (bit 8) kept it (l6-s2-own-to-standard). The library kept the old style's arrow.
            DxfMLeader taken = Words(Drawing());
            SetOwn(taken, 340, "FFFF");
            SetOwn(taken, 342, "B5");
            taken.MLeaderStyle = "Standard";
            Assert.Null(After(taken, "301", 342));
            Assert.Null(taken.ArrowSymbol);

            DxfMLeader own = Words(Drawing());
            SetOwn(own, 340, "FFFF");
            SetOwn(own, 342, "B5");
            SetOwn(own, 90, (Flags(own) | (1 << 8)).ToString(CultureInfo.InvariantCulture));
            own.MLeaderStyle = "Standard";
            Assert.Equal("B5", After(own, "301", 342));
        }

        [Fact]
        public void OneOfTwoLeadersOfASideGivenItsDogleg_LandsAnew_TheWordsStaying_AsAutoCadDoes()
        {
            // AutoCAD 2021, words at (160, 40) met on the left by two leaders landed at (150, 38): SetDoglegLength(1, 15) moved the
            // second one's start to (143, 38), the words and the first staying (b-two-landed-setdl1-15); DoglegLength 15 moved
            // both, the words staying (b-two-landed-dl15). The library moved nothing for the first, and the words for the second.
            DxfMLeader one = AutoCads(Drawing(), TwoLeft);
            one.SetDoglegLength(1, 15);
            Near(P(160, 40), one.TextLocation);
            Near(P(150, 38), one.GetLastVertex(0));
            Near(P(143, 38), one.GetLastVertex(1));

            DxfMLeader all = AutoCads(Drawing(), TwoLeft);
            all.DoglegLength = 15;
            Near(P(160, 40), all.TextLocation);
            Near(P(143, 38), all.GetLastVertex(0));
            Near(P(143, 38), all.GetLastVertex(1));
        }
    }
}
