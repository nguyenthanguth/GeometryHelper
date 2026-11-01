using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The landing of a MULTILEADER as AutoCAD 2021 makes, saves, reads and explodes it (plugin D31, rounds A, D, G and H, on a
    /// Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8), and an INSERT of a block its drawing has not inserted
    /// with another drawing, as AutoCAD 2021's <c>Insert</c> copies it (rounds C, E and F).
    /// </summary>
    public class DxfD31Tests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        // AutoCAD 2021's records (after AcDbMLeader, its proxy graphics left out) of words "ABC" 4 high held at (100, 50), a line
        // from (0, 0) and a leader whose dogleg of 8 runs from (90, 48): its EnableDogleg made false (d07)...
        private const string NoDogleg = "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 296000|170 1|91 -1056964608|341 5B|171 -2|290 1|291 0|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... its leader lines splined, a second line from (0, 100) (d19) ...
        private const string Splined = "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|304 LEADER_LINE{|10 0.0|20 100.0|30 0.0|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295937|170 2|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ... words 60 wide, the line's arrows 2 of its own, then Scale set to 2 (h1c) ...
        private const string ScaledTwice = "270 2|300 CONTEXT_DATA{|40 2.0|10 106.0|20 48.0|30 0.0|41 8.0|140 8.0|145 4.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 110.0|22 52.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 120.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 16.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|40 2.0|93 16|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 17073152|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 2.0|271 0|272 9|273 9|295 1";

        // ... three lines of words met at their top or bottom, a line from (100, -50) (d15) ...
        private const string Vertical = "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 A\\PBB\\PCCC|11 0.0|21 0.0|31 1.0|340 55|12 92.66666666666667|22 67.33333333333333|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 98.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 100.0|20 -50.0|30 0.0|91 0|305 }|271 1|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 1|272 9|273 9|295 1";

        // ... and, on a Standard of words 0.18 high, gaps of 0.09 and doglegs of 0.36, its DoglegLength made nought (c08).
        private const string NoLength = "270 2|300 CONTEXT_DATA{|40 1.0|10 99.90999999999999|20 49.90999999999999|30 0.0|41 0.2|140 0.18|145 0.09|174 1|175 6|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.01|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 99.90999999999999|20 49.90999999999999|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 0.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 361600|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 0.0|42 0.18|172 2|343 55|173 1|95 6|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        private static DxfDatabase Drawing() => DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

        /// <summary>
        /// A multileader of the drawing given AutoCAD 2021's record: one made and added, then its groups after its AcDbMLeader
        /// marker replaced by AutoCAD's, its style (AutoCAD's 67) and text style (55) the drawing's, its linetype (5B) left out.
        /// </summary>
        private static DxfMLeader AutoCads(DxfDatabase db, string record)
        {
            var made = new DxfMLeader { MText = new DxfMText { Contents = "X", Location = new GeoPoint3(0, 0, 0), TextHeight = 4.0 } };
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

                value = code == 340 && value == "67" ? style : (code == 340 || code == 343) && value == "55" ? text : value;
                groups.Add(new KeyValuePair<int, string>(code, value));
            }

            made.Item.File.ReplaceTail(made.Item, 0, groups);
            return made;
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        /// <summary>How long a closed filled arrow is: from its tip (its second corner) to the middle of its back (the first and third).</summary>
        private static double Length(DxfSolid arrow)
        {
            GeoPoint3 a = arrow.GetPointAt(0);
            GeoPoint3 c = arrow.GetPointAt(2);
            return arrow.GetPointAt(1).DistanceTo(new GeoPoint3((a.X + c.X) / 2, (a.Y + c.Y) / 2, (a.Z + c.Z) / 2));
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        /// <summary>
        /// The number of the first group of a code in the multileader's record after the first group of another code (301, the
        /// end of the context data) or value (<c>LEADER{</c>); null where it has none.
        /// </summary>
        private static double? After(DxfEntity entity, string from, int code)
        {
            List<DxfTypedValue> tail = entity.Item.File.TailOf(entity.Item, 0);
            int start = tail.FindIndex(g => int.TryParse(from, out int at) ? g.TypeCode == at : g.Value.Trim() == from);
            List<DxfTypedValue> found = tail.Skip(start + 1).Where(g => g.TypeCode == code).Take(1).ToList();
            return found.Count == 0 ? (double?)null : double.Parse(found[0].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>A multileader made in code as GHD31 made AutoCAD's: words "ABC" 4 high held at (100, 50), by the library's defaults (gap 2, dogleg 8, the middle of the top line).</summary>
        private static DxfMLeader Words(DxfDatabase db, string words = "ABC", params GeoPoint3[] points)
        {
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = words, Location = P(100, 50), TextHeight = 4.0 } };

            foreach (GeoPoint3 point in points.Length == 0 ? new[] { P(0, 0) } : points)
            {
                mleader.AddLeaderLine(point);
            }

            db.ModelSpace.AppendEntity(mleader);
            return mleader;
        }

        // ---- reading AutoCAD's landing -------------------------------------------------------------------------------------

        [Fact]
        public void ALeaderWhoseDoglegIsTakenAway_EndsItsLines_WhereTheDoglegWouldEnd_AsAutoCadReadsIt()
        {
            // AutoCAD 2021 gave GetLastVertex (98, 48) and exploded a polyline from the arrow to (98, 48) and no LINE (d07); the
            // library gave the dogleg's start, (90, 48).
            DxfMLeader mleader = AutoCads(Drawing(), NoDogleg);

            Near(P(98, 48), mleader.GetLastVertex(0));
            Assert.Equal(2, mleader.VerticesCount(0));
            IReadOnlyList<DxfEntity> parts = mleader.Explode();
            Assert.DoesNotContain(parts, p => p is DxfLine);
            DxfPolyline line = parts.OfType<DxfPolyline>().Single();
            Near(P(98, 48), line.GetPoint3dAt(line.NumberOfVertices - 1));
        }

        [Fact]
        public void ALastVertexSetOrAddedWithNoDogleg_PutsTheDoglegsStartADoglegShortOfIt_AsAutoCadPutsIt()
        {
            // AutoCAD 2021: SetLastVertex(0, (50, 60)) with no dogleg wrote the leader's 10 at (42, 60) (g2); AddLastVertex(0,
            // (70, 30)) gave the vertices (0, 0), (98, 48), (70, 30) and wrote 10 at (62, 30) (g3).
            DxfMLeader set = AutoCads(Drawing(), NoDogleg);
            set.SetLastVertex(0, P(50, 60));
            Assert.Equal(new double?[] { 42, 60 }, new[] { After(set, "LEADER{", 10), After(set, "LEADER{", 20) });
            Near(P(50, 60), set.GetLastVertex(0));

            DxfMLeader added = AutoCads(Drawing(), NoDogleg);
            added.AddLastVertex(0, P(70, 30));
            Assert.Equal(3, added.VerticesCount(0));
            Near(P(98, 48), added.GetVertex(0, 1));
            Near(P(70, 30), added.GetLastVertex(0));
            Assert.Equal(new double?[] { 62, 30 }, new[] { After(added, "LEADER{", 10), After(added, "LEADER{", 20) });

            // The first vertex moved leaves the leader's point as it was, to the bit.
            DxfMLeader first = AutoCads(Drawing(), NoDogleg);
            first.SetFirstVertex(0, P(1, 1));
            Assert.Equal(new double?[] { 90, 48 }, new[] { After(first, "LEADER{", 10), After(first, "LEADER{", 20) });
        }

        [Fact]
        public void AMultileaderOfSplinedLines_IsExplodedWithNoDogleg_ItsSplinesRunToWhereTheDoglegWouldEnd()
        {
            // AutoCAD 2021 (d19): EnableDogleg false though the file says 291 1; two SPLINEs of two fit points each, the backs of
            // their arrows and (98, 48), and no LINE; GetLastVertex (98, 48). The library drew a dogleg from (90, 48) and splines
            // to (90, 48).
            DxfMLeader mleader = AutoCads(Drawing(), Splined);

            IReadOnlyList<DxfEntity> parts = mleader.Explode();
            Assert.DoesNotContain(parts, p => p is DxfLine);
            List<DxfSpline> splines = parts.OfType<DxfSpline>().ToList();
            Assert.Equal(2, splines.Count);
            Assert.All(splines, s => Near(P(98, 48), s.GetFitPointAt(s.NumFitPoints - 1)));
            Near(P(3.592250831948852, 1.759469795240254), splines[0].GetFitPointAt(0));
            Near(P(98, 48), mleader.GetLastVertex(1));

            // Read as a curve, the same: each line ends at (98, 48).
            Assert.All(mleader.GetGeometry().OfType<GeoSpline3>(), s => Near(P(98, 48), s.EndPoint));
        }

        [Fact]
        public void ADoglegOfNoLength_IsExplodedAsALineOfNoLength_AsAutoCadExplodesIt()
        {
            // AutoCAD 2021 (c08): a LINE from (99.91, 49.91) to (99.91, 49.91) first, then the polyline and the arrow.
            DxfMLeader mleader = AutoCads(Drawing(), NoLength);

            DxfLine dogleg = mleader.Explode().OfType<DxfLine>().Single();
            Near(P(99.91, 49.91), dogleg.StartPoint);
            Near(P(99.91, 49.91), dogleg.EndPoint);
        }

        [Fact]
        public void ALeadersDoglegLength_IsItsDrawnLengthOverTheScale_AndNoughtAtTheTopOrBottomOfTheWords()
        {
            // AutoCAD 2021: GetDoglegLength 8 for a dogleg drawn 16 at a scale of 2 (h1c), and nought for a leader met at the
            // bottom of the words though its 40 says 8 (d15). The library gave 16 and 8.
            Assert.Equal(8.0, AutoCads(Drawing(), ScaledTwice).GetDoglegLength(0));
            Assert.Equal(0.0, AutoCads(Drawing(), Vertical).GetDoglegLength(0));
        }

        [Fact]
        public void TheWordsOfAScaledMultileader_AreExplodedAsWideAsTheRecordSays()
        {
            // AutoCAD 2021 (h1c): words 60 wide scaled by 2 are written 120 wide (43) and exploded 120 wide; the line's own arrows
            // of 2 are drawn 4. The library exploded them 240 wide.
            DxfMLeader mleader = AutoCads(Drawing(), ScaledTwice);

            IReadOnlyList<DxfEntity> parts = mleader.Explode();
            Assert.Equal(120.0, parts.OfType<DxfMText>().Single().Width);
            DxfSolid arrow = parts.OfType<DxfSolid>().Single();
            Assert.Equal(4.0, Length(arrow), 6);
        }

        [Fact]
        public void TheArrowheadsAndContent_AreExplodedInTheMultileadersOwnLook_AsAutoCadExplodesThem()
        {
            // AutoCAD 2021: of a multileader of colour 3, lineweight 0.30 and linetype Continuous of its own, the words and the
            // block are 0.30 and Continuous (d17, h2); the SOLID is 0.30 and Continuous, an INSERT of an arrow block takes the
            // leader lines' lineweight, 0.50 (g1, g1b, d18), turned 3.6315 (not -2.6516). The library gave the SOLID the
            // lines' linetype, the words and the block none of the multileader's own.
            DxfDatabase db = Drawing();
            DxfMLeader mleader = Words(db);
            mleader.ColorIndex = 3;
            mleader.LineWeight = DxfLineWeight.LineWeight030;
            mleader.Linetype = "Continuous";
            mleader.LeaderLineWeight = DxfLineWeight.LineWeight050;

            IReadOnlyList<DxfEntity> parts = mleader.Explode();
            DxfMText words = parts.OfType<DxfMText>().Single();
            Assert.Equal(DxfLineWeight.LineWeight030, words.LineWeight);
            Assert.Equal("Continuous", words.Linetype);
            DxfSolid solid = parts.OfType<DxfSolid>().Single();
            Assert.Equal(DxfLineWeight.LineWeight030, solid.LineWeight);
            Assert.Equal("Continuous", solid.Linetype);
            Assert.Equal(DxfLineWeight.LineWeight050, parts.OfType<DxfPolyline>().Single().LineWeight);

            mleader.ArrowSymbol = db.BlockTable["GKTAG"];
            DxfBlockReference arrow = mleader.Explode().OfType<DxfBlockReference>().Single();
            Assert.Equal(DxfLineWeight.LineWeight050, arrow.LineWeight);
            Assert.Equal("Continuous", arrow.Linetype);
            Assert.InRange(arrow.Rotation, 0.0, 2 * Math.PI);
            Assert.Equal(Math.Atan2(-48.0, -90.0) + 2 * Math.PI, arrow.Rotation, 9);
        }

        [Fact]
        public void TheBlockOfAMultileader_IsExplodedInItsOwnLineweightAndLinetype()
        {
            // AutoCAD 2021 (h2): the INSERT of the block of a multileader of 0.30 and Continuous is 0.30 and Continuous.
            DxfDatabase db = Drawing();
            DxfMLeader block = db.ModelSpace.OfType<DxfMLeader>().Single(m => m.ContentType == DxfContentType.BlockContent);
            block.LineWeight = DxfLineWeight.LineWeight030;
            block.Linetype = "Continuous";

            DxfBlockReference content = block.Explode().OfType<DxfBlockReference>().First();
            Assert.Equal(DxfLineWeight.LineWeight030, content.LineWeight);
            Assert.Equal("Continuous", content.Linetype);
        }

        // ---- landing a leader made in code ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(DxfTextAttachmentType.AttachmentTopOfTop, 50.0)]
        [InlineData(DxfTextAttachmentType.AttachmentMiddleOfTop, 48.0)]
        [InlineData(DxfTextAttachmentType.AttachmentMiddle, 41.333333333333336)]
        [InlineData(DxfTextAttachmentType.AttachmentMiddleOfBottom, 34.666666666666671)]
        [InlineData(DxfTextAttachmentType.AttachmentBottomOfBottom, 32.666666666666671)]
        [InlineData(DxfTextAttachmentType.AttachmentBottomLine, 31.866666666666667)]
        [InlineData(DxfTextAttachmentType.AttachmentBottomOfTopLine, 45.200000000000003)]
        [InlineData(DxfTextAttachmentType.AttachmentBottomOfTop, 46.0)]
        [InlineData(DxfTextAttachmentType.AttachmentAllLine, 45.200000000000003)]
        public void ALeaderAddedOnTheLeftOfThreeLinesOfWords_IsLandedWhereAutoCadLandsIt(DxfTextAttachmentType attachment, double y)
        {
            // AutoCAD 2021 (d04): words A, BB, CCC 4 high at (100, 50), met on the left as given, a line from (0, 0): the dogleg
            // of 8 from (90, y) to (98, y). The library started it at the words' place, (100, 50).
            var mleader = new DxfMLeader();
            mleader.SetTextAttachmentType(attachment, DxfLeaderDirectionType.LeftLeader);
            mleader.MText = new DxfMText { Contents = "A\\PBB\\PCCC", Location = P(100, 50), TextHeight = 4.0 };
            mleader.AddLeaderLine(P(0, 0));
            Drawing().ModelSpace.AppendEntity(mleader);

            Near(P(90, y), mleader.GetLastVertex(0));
            Assert.True(mleader.GetDogleg(0).IsEqualTo(new GeoVector3(1, 0, 0), Close));
            DxfLine dogleg = mleader.Explode().OfType<DxfLine>().Single();
            Near(P(98, y), dogleg.EndPoint);
        }

        [Fact]
        public void ALeaderAddedOnTheLeft_IsLandedTheGapAndTheDoglegShortOfTheWords_FromAnyPointOnTheirLeft()
        {
            // AutoCAD 2021: (90, 48) from (0, 0) (d01) and from (50, 100) above the words (d11); with a gap of 5 and a dogleg of 15
            // set first, (80, 48) (d06). A second line from the left joins the leader (c03b).
            DxfMLeader plain = Words(Drawing());
            Near(P(90, 48), plain.GetLastVertex(0));
            Near(P(90, 48), Words(Drawing(), "ABC", P(50, 100)).GetLastVertex(0));

            var wide = new DxfMLeader { LandingGap = 5, DoglegLength = 15 };
            wide.MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0 };
            wide.AddLeaderLine(P(0, 0));
            Near(P(80, 48), wide.GetLastVertex(0));
            Assert.Equal(15.0, wide.GetDoglegLength(0));

            int second = plain.AddLeaderLine(P(0, 100));
            Assert.Equal(1, plain.LeaderCount);
            Near(P(90, 48), plain.GetLastVertex(second));
        }

        [Fact]
        public void WordsMoved_TakeTheLandingsWithThem_AsAutoCadMovesThem()
        {
            // AutoCAD 2021 (d08, g4): words moved from (100, 50) to (120, 80) moved the dogleg from (90, 48) to (110, 78), the line's
            // first vertex staying at (0, 0). The library left the dogleg where it was. The base point of the content moves too,
            // the words' place in a multileader made in code.
            DxfMLeader mleader = Words(Drawing());
            mleader.TextLocation = P(120, 80);

            Near(P(110, 78), mleader.GetLastVertex(0));
            Near(P(0, 0), mleader.GetFirstVertex(0));
            Assert.Equal(new double?[] { 120, 80 }, new[] { After(mleader, "CONTEXT_DATA{", 10), After(mleader, "CONTEXT_DATA{", 20) });
        }

        [Fact]
        public void TheLengthOfTheDoglegs_IsWrittenAsAutoCadWritesIt_UnscaledForTheMultileader_ScaledForEachLeader()
        {
            // AutoCAD 2021: Scale 2 left the multileader's 41 at 8 and drew the dogleg 16 (d12); DoglegLength 3 then gave 41 of 3
            // and a dogleg of 6 (d12c); SetDoglegLength(0, 1) a dogleg of 2 (d12b). The library scaled 41 too, and wrote the
            // lengths given unscaled.
            DxfMLeader mleader = Words(Drawing());
            mleader.Scale = 2;
            Assert.Equal(8.0, mleader.DoglegLength);
            Assert.Equal(new double?[] { 8, 16 }, new[] { After(mleader, "301", 41), After(mleader, "LEADER{", 40) });
            Assert.Equal(8.0, mleader.GetDoglegLength(0));

            mleader.DoglegLength = 3;
            Assert.Equal(new double?[] { 3, 6 }, new[] { After(mleader, "301", 41), After(mleader, "LEADER{", 40) });

            mleader.SetDoglegLength(0, 1);
            Assert.Equal(2.0, After(mleader, "LEADER{", 40));
            Assert.Equal(1.0, mleader.GetDoglegLength(0));
        }

        [Fact]
        public void AScaleSet_ScalesTheWidthOfTheWords_AsAutoCadScalesIt()
        {
            // AutoCAD 2021 (h1c): words 60 wide at a scale of 2 are written 120 wide.
            DxfMLeader mleader = Words(Drawing());
            DxfMText words = mleader.MText;
            words.Width = 60;
            mleader.MText = words;

            mleader.Scale = 2;

            Assert.Equal(120.0, mleader.MText.Width);
            Assert.Equal(120.0, mleader.Explode().OfType<DxfMText>().Single().Width);
        }

        [Fact]
        public void AMultileaderScaledByATransformation_KeepsItsScaleAndItsOwnSizes_AsAutoCadKeepsThem()
        {
            // AutoCAD 2021 (g5, h1): scaled by 2 about the origin, the scale stays 1 (40 of the context, 45), the multileader's own
            // dogleg 8 (41) and arrows 4 (42); the dogleg is drawn 16, GetDoglegLength 16, the words 120 wide, and a line's own
            // arrows of 2 still 2. The library made the scale 2, 41 16 and 42 8.
            DxfMLeader mleader = Words(Drawing());
            DxfMText words = mleader.MText;
            words.Width = 60;
            mleader.MText = words;
            mleader.SetArrowSize(0, 2);

            mleader.TransformBy(GeoTransform3.Scaling(new GeoPoint3(0, 0, 0), 2));

            Assert.Equal(1.0, mleader.Scale);
            Assert.Equal(8.0, mleader.DoglegLength);
            Assert.Equal(4.0, mleader.ArrowSize);
            Assert.Equal(16.0, mleader.GetDoglegLength(0));
            Near(P(180, 96), mleader.GetLastVertex(0));
            IReadOnlyList<DxfEntity> parts = mleader.Explode();
            Assert.Equal(120.0, parts.OfType<DxfMText>().Single().Width);
            DxfLine dogleg = parts.OfType<DxfLine>().Single();
            Near(P(196, 96), dogleg.EndPoint);
            DxfSolid arrow = parts.OfType<DxfSolid>().Single();
            Assert.Equal(2.0, Length(arrow), 6);
        }

        // ---- inserting a drawing of INSERTs of blocks it has not -----------------------------------------------------------

        private const string Head = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
        private const string Line = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";

        private static string Insert(string name) => "  0\nINSERT\n  8\n0\n  2\n" + name + "\n 10\n0\n 20\n0\n 30\n0\n";

        private static string Block(string name, string entities) => "  0\nBLOCK\n  8\n0\n  2\n" + name + "\n 70\n" + (name.StartsWith("*", StringComparison.Ordinal) ? "1" : "0") + "\n 10\n0\n 20\n0\n 30\n0\n" + entities + "  0\nENDBLK\n  8\n0\n";

        private static string R12(string blocks, string entities) => Head + blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n" + entities + "  0\nENDSEC\n  0\nEOF\n";

        private static List<string> Logged(Action action)
        {
            var written = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            bool enabled = GeometryHelperLog.Enable;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add(level + " " + message);
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        private static string[] Placed(DxfBlockTableRecord block) => block.OfType<DxfBlockReference>().Select(r => r.Name).ToArray();

        [Fact]
        public void ADrawingInsertedWithAnInsertOfABlockItHasNot_InABlockItMerges_IsCopiedAsItIs_AsAutoCadInsertsIt()
        {
            // AutoCAD 2021's Insert, both ways, copied an INSERT of a block erased under it in M as an INSERT of no block (E, C),
            // as it copied one in an anonymous block; the library refused M's ("The drawing has no block Z") and copied *U1's.
            DxfDatabase other = DxfDatabase.Parse(R12(Block("M", Line + Insert("Z")), Insert("M")));
            DxfDatabase db = DxfDatabase.Parse(R12(Block("P", Line), string.Empty));

            List<string> logged = Logged(() => db.Insert(GeoTransform3.Identity, other, true));

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "Z" }, Placed(read.BlockTable["M"]));
            Assert.Equal(new[] { "M" }, Placed(read.ModelSpace));
            Assert.False(read.BlockTable.Has("Z"));
            Assert.Contains(logged, m => m.StartsWith("Debug ", StringComparison.Ordinal) && m.Contains("1 INSERT") && m.Contains("(Z)"));

            DxfDatabase named = DxfDatabase.Parse(R12(Block("P", Line), string.Empty));
            named.Insert("NEWB", other, true);
            Assert.Equal(new[] { "M" }, Placed(named.BlockTable["NEWB"]));
            Assert.Equal(new[] { "Z" }, Placed(named.BlockTable["M"]));
        }

        [Fact]
        public void ADrawingInsertedWithAnInsertOfABlockItHasNot_InItsModelSpace_IsCopiedAsItIs_AsAutoCadInsertsIt()
        {
            // AutoCAD 2021's Insert, both ways, copied an INSERT of model space whose block was erased (E, model); the library
            // refused the drawing.
            DxfDatabase other = DxfDatabase.Parse(R12(Block("A", Line), Insert("Z") + Line));
            DxfDatabase db = DxfDatabase.Parse(R12(Block("P", Line), string.Empty));

            db.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), other, true);

            DxfBlockReference copy = db.ModelSpace.OfType<DxfBlockReference>().Single();
            Assert.Equal("Z", copy.Name);
            Assert.True(copy.Position.IsEqualTo(P(100, 0), Close), copy.Position.ToString());
            Assert.Equal(new[] { "Z" }, Placed(db.Insert("NEWB", other, true)));
        }

        [Fact]
        public void AnInsertOfABlockItsDrawingHasNot_InsertedIntoOneThatHasTheBlock_PlacesThatBlock_AndIsSaidSo()
        {
            // AutoCAD 2021 left such a copy of no block even where the drawing has a Z (E); a DXF names its block by name, so the
            // copy places this drawing's Z, as an INSERT a copy brings in a block does, and the Debug line says it places one.
            DxfDatabase other = DxfDatabase.Parse(R12(Block("M", Insert("Z")), Insert("M")));
            DxfDatabase db = DxfDatabase.Parse(R12(Block("Z", Line), string.Empty));

            List<string> logged = Logged(() => db.Insert(GeoTransform3.Identity, other, true));

            Assert.Equal(new[] { "Z" }, Placed(db.BlockTable["M"]));
            Assert.Contains(logged, m => m.Contains("1 of them places a block this drawing has"));
        }

        [Fact]
        public void AnInsertOfABlockGivenToBeCopiedAlone_ThatItsDrawingHasNot_IsStillRefused_AsAutoCadsWblockCloneObjectsRefusesIt()
        {
            // AutoCAD 2021's WblockCloneObjects threw eWasErased for a set holding or reaching an INSERT of an erased block (E);
            // the library refuses one given among the entities, and copies those in the blocks the copies bring (D10).
            DxfDatabase other = DxfDatabase.Parse(R12(Block("A", Line), Insert("Z")));
            DxfDatabase db = DxfDatabase.Parse(R12(Block("P", Line), string.Empty));
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => other.WblockCloneObjects(other.ModelSpace.ToList(), db.ModelSpace));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ADrawingMadeABlockWhoseBlocksInsertItsNewName_IsRefusedAsARound_BeforeAnythingIsWritten()
        {
            // The other's A holds an INSERT of NEWB, which it has not, and its model space an INSERT of A: made the block NEWB,
            // NEWB would place A and A NEWB. Copied as it is now, the INSERT is walked for a round and the call refused.
            DxfDatabase other = DxfDatabase.Parse(R12(Block("A", Line + Insert("NEWB")), Insert("A")));
            DxfDatabase db = DxfDatabase.Parse(R12(Block("P", Line), string.Empty));
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => db.Insert("NEWB", other, true));

            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());

            // Under another name, A is merged with its INSERT of NEWB, which places nothing.
            Assert.Equal(new[] { "A" }, Placed(db.Insert("PLATE", other, true)));
            Assert.Equal(new[] { "NEWB" }, Placed(db.BlockTable["A"]));
        }
    }
}
