using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The multileader leftovers of F1b, F2 and D37, as AutoCAD 2021 answered them (plugin D38, headless): words mirrored across
    /// their plane, which run against its x axis, kept landed; the font a new leader is landed by where the words' text style is
    /// not the multileader style's; the leaders run on to the words (295) of a new multileader; a new leader line of no colour of
    /// its own; and bits 14, 15 and 18 of the overrides. Fonts are those of F1b's tests, written in code: txt.shx's capitals
    /// inked 4 by 6 and moving the pen 6, its g reaching 2 below the baseline, so "ABC" 4 high is 10.67 wide.
    /// </summary>
    public class DxfMLeaderD38Tests
    {
        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        // ---- the fonts, as F1b's tests write them ---------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down from the origin up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>
        /// The bytes of a unifont of AutoCAD's form, 6 above and 2 below as txt.shx is: a space, the capitals and g, its g
        /// reaching 2 below the baseline, or 3 where it is deep.
        /// </summary>
        private static byte[] Txt(bool deep = false)
        {
            byte[] g = deep
                ? new byte[] { 1, 0x3C, 0x40, 0x74, 2, 0x4C, 0x20, 0 }
                : new byte[] { 1, 0x2C, 0x40, 0x64, 2, 0x4C, 0x20, 0 };
            var letters = new Dictionary<int, byte[]> { { ' ', new byte[] { 2, 0x60, 0 } }, { 'g', g } };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters.OrderBy(l => l.Key))
            {
                byte[] record = new byte[] { 0 }
                    .Concat(letter.Value)
                    .ToArray();
                bytes.AddRange(BitConverter.GetBytes((ushort)letter.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Length));
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        /// <summary>A folder of its own holding txt.shx and deep.shx (<see cref="Txt"/>) and gh.ttf (<see cref="TrueTypeFile.Letters"/>); deleted after the test.</summary>
        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-d38-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Txt());
                File.WriteAllBytes(Path.Combine(folder, "deep.shx"), Txt(true));
                File.WriteAllBytes(Path.Combine(folder, "gh.ttf"), TrueTypeFile.Font(TrueTypeFile.Letters()));
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // ---- the drawings -----------------------------------------------------------------------------------------------------------

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Is(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(expected.Subtract(actual).Length <= 1E-9, "expected " + expected + ", got " + actual);

        private static GeoTransform3 Mirror(double x, double y, double z) =>
            GeoTransform3.Mirror(new GeoPlane3(P(0, 0), new GeoVector3(x, y, z)));

        /// <summary>A drawing of a Standard multileader style, its fonts looked for in the folder given (none where null).</summary>
        private static DxfDatabase Leaders(string folder)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            if (folder != null)
            {
                db.FontSearchPath = folder;
            }

            return db;
        }

        /// <summary>A multileader made in code as D38's plugin made AutoCAD's: words 4 high at (100, 50) on the Standard of a gap of 2 and doglegs of 8, met at the middle of their top line, its lines from the points given.</summary>
        private static DxfMLeader Made(DxfDatabase db, string words, params GeoPoint3[] from)
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
            db.ModelSpace.AppendEntity(mleader);
            mleader.LandingGap = 2;
            mleader.DoglegLength = 8;
            mleader.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentMiddleOfTop,
                DxfLeaderDirectionType.LeftLeader
            );
            mleader.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentMiddleOfTop,
                DxfLeaderDirectionType.RightLeader
            );
            mleader.TextLocation = P(100, 50);

            foreach (GeoPoint3 point in from)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        /// <summary>"ABC" (or the words given) met from the points given, then mirrored across the XY plane: held at (110.67, 50), facing down and running against x.</summary>
        private static DxfMLeader Mirrored(DxfDatabase db, string words, params GeoPoint3[] from)
        {
            DxfMLeader mleader = Made(db, words, from);
            mleader.TransformBy(Mirror(0, 0, 1));
            return mleader;
        }

        private static readonly GeoPoint3[] Left = { P(0, 0) };
        private static readonly GeoPoint3[] Right = { P(250, 0) };
        private static readonly GeoPoint3[] Both = { P(0, 0), P(250, 0) };

        private static List<DxfTypedValue> Tail(DxfEntity entity) => entity.Item.File.TailOf(entity.Item, 0);

        /// <summary>The overrides of the style, the 90 after the context data.</summary>
        private static int Flags(DxfMLeader mleader)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int end = tail.FindIndex(g => g.TypeCode == 301);
            return int.Parse(
                tail.Skip(end + 1).First(g => g.TypeCode == 90).Value.Trim(),
                CultureInfo.InvariantCulture
            );
        }

        /// <summary>The value of a group after the context data; null where there is none.</summary>
        private static string Own(DxfMLeader mleader, int code)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int end = tail.FindIndex(g => g.TypeCode == 301);
            return tail.Skip(end + 1).Where(g => g.TypeCode == code).Select(g => g.Value.Trim()).FirstOrDefault();
        }

        /// <summary>The value of a group of the head of the context data, before its MTEXT, block and leaders; null where there is none.</summary>
        private static string Head(DxfMLeader mleader, int code)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int start = tail.FindIndex(g => g.TypeCode == 300);
            int end = tail.FindIndex(g => g.TypeCode == 302 || g.TypeCode == 301);
            return tail.Skip(start + 1)
                .Take(end - start - 1)
                .Where(g => g.TypeCode == code)
                .Select(g => g.Value.Trim())
                .LastOrDefault();
        }

        /// <summary>The base point of the content, 10 of the context data.</summary>
        private static GeoPoint3 BasePoint(DxfMLeader mleader)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int start = tail.FindIndex(g => g.TypeCode == 300);
            int at = tail.FindIndex(start, g => g.TypeCode == 10);
            return new GeoPoint3(Num(tail[at].Value), Num(tail[at + 1].Value), Num(tail[at + 2].Value));
        }

        /// <summary>The value of a group of the context's MTEXT; null where there is none.</summary>
        private static string Words(DxfMLeader mleader, int code)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int start = tail.FindIndex(g => g.TypeCode == 304);
            int end = tail.FindIndex(start, g => g.TypeCode == 296);
            return tail.Skip(start)
                .Take(end - start)
                .Where(g => g.TypeCode == code)
                .Select(g => g.Value.Trim())
                .FirstOrDefault();
        }

        /// <summary>The groups of each LEADER_LINE, as "code value" texts.</summary>
        private static List<List<string>> LeaderLines(DxfMLeader mleader)
        {
            var lines = new List<List<string>>();
            List<string> line = null;

            foreach (DxfTypedValue g in Tail(mleader))
            {
                string text = g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim();

                if (text == "304 LEADER_LINE{")
                {
                    line = new List<string>();
                    lines.Add(line);
                }
                else if (text == "305 }")
                {
                    line = null;
                }
                else
                {
                    line?.Add(text);
                }
            }

            return lines;
        }

        private static double Num(string text) => double.Parse(text.Trim(), CultureInfo.InvariantCulture);

        private static bool Bit(DxfMLeader mleader, int bit) => (Flags(mleader) & (1 << bit)) != 0;

        /// <summary>
        /// A MULTILEADER of AutoCAD 2021's own record, as F1b's tests make one: its groups after AcDbMLeader those given, the
        /// drawing's own style and text style put in for AutoCAD's handles 67 and 55.
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

        // AutoCAD 2021's record of "ABC" 4 high at (100, 50) met from (0, 0), mirrored across the XY plane (D38 m-mxy).
        private const string MirroredRecord =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 -1.0|340 55|12 110.6666666666666|22 50.0|32 0.0|13 -1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 -1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 1|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        // ---- item 1: words that run against the plane's x axis -------------------------------------------------------------------

        [Fact]
        public void MirroredWordsMetOnTheLeft_MoveWithTheirDogleg_GapAndAttachment_AsAutoCadMovesThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 m-mxy-*): "ABC" held at (110.67, 50), running against x from the leader's landing at (90, 48).
                DxfMLeader dogleg = Mirrored(db, "ABC", Left);
                dogleg.DoglegLength = 15;
                Is(P(117.66666666666667, 50), dogleg.TextLocation);
                Is(P(90, 48), dogleg.GetLastVertex(0));

                DxfMLeader own = Mirrored(db, "ABC", Left);
                own.SetDoglegLength(0, 15);
                Is(P(117.66666666666667, 50), own.TextLocation);

                DxfMLeader gap = Mirrored(db, "ABC", Left);
                gap.LandingGap = 5;
                Is(P(113.66666666666667, 50), gap.TextLocation);

                DxfMLeader top = Mirrored(db, "ABC", Left);
                top.SetTextAttachmentType(DxfTextAttachmentType.AttachmentTopOfTop, DxfLeaderDirectionType.LeftLeader);
                Is(P(110.66666666666667, 48), top.TextLocation);

                DxfMLeader under = Mirrored(db, "ABC", Left);
                under.SetTextAttachmentType(
                    DxfTextAttachmentType.AttachmentBottomOfTopLine,
                    DxfLeaderDirectionType.LeftLeader
                );
                Is(P(110.66666666666667, 52.8), under.TextLocation);

                DxfMLeader moved = Mirrored(db, "ABC", Left);
                moved.SetLastVertex(0, P(40, 70));
                Is(P(60.666666666666671, 72), moved.TextLocation);
            });
        }

        [Fact]
        public void MirroredWordsMetOnTheLeft_KeepTheirLeftEdge_AsTheyGrow_AsAutoCadKeepsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38): 10 high, 26.67 wide from x = 100, held at (126.67, 53); at a scale of 2, from 110, at
                // (131.33, 52), the base point moved 8 with the dogleg; "ABCDEF" from 100 at (122.67, 50).
                DxfMLeader high = Mirrored(db, "ABC", Left);
                high.TextHeight = 10;
                Is(P(126.66666666666666, 53), high.TextLocation);
                Is(P(90, 48), high.GetLastVertex(0));

                DxfMLeader scaled = Mirrored(db, "ABC", Left);
                GeoPoint3 was = BasePoint(scaled);
                scaled.Scale = 2;
                Is(P(131.33333333333334, 52), scaled.TextLocation);
                Is(was.Add(new GeoVector3(8, 0, 0)), BasePoint(scaled));

                DxfMLeader anew = Mirrored(db, "ABC", Left);
                DxfMText words = anew.MText;
                words.Contents = "ABCDEF";
                anew.MText = words;
                Is(P(122.66666666666666, 50), anew.TextLocation);

                DxfMLeader all = Mirrored(db, "ABC", Left);
                all.DoglegLength = 15;
                all.LandingGap = 5;
                all.TextHeight = 10;
                Is(P(136.66666666666666, 53), all.TextLocation);

                // "Ag", its g below the baseline, 10 high: 16.67 wide from 100, met 6.67 below its top for 2.67 at 4 high.
                DxfMLeader deep = Mirrored(db, "Ag", Left);
                Is(P(90, 47.333333333333336), deep.GetLastVertex(0));
                deep.TextHeight = 10;
                Is(P(116.66666666666667, 54), deep.TextLocation);
            });
        }

        [Fact]
        public void MirroredWordsMetOnTheRight_KeepTheirRightEdgeOnTheLanding_AsAutoCadKeepsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 m-r-mxy-*): held at their right edge, (110.67, 50), the landing at (120.67, 48); the base
                // point of the content moved with their left edge, by 16 as they are made 10 high, by 22.67 at a scale of 2 and
                // by 12 for "ABCDEF".
                DxfMLeader dogleg = Mirrored(db, "ABC", Right);
                Is(P(120.66666666666667, 48), dogleg.GetLastVertex(0));
                dogleg.DoglegLength = 15;
                Is(P(103.66666666666667, 50), dogleg.TextLocation);

                DxfMLeader gap = Mirrored(db, "ABC", Right);
                gap.LandingGap = 5;
                Is(P(113.66666666666667, 50), gap.TextLocation);
                Is(P(126.66666666666666, 48), gap.GetLastVertex(0));

                DxfMLeader high = Mirrored(db, "ABC", Right);
                GeoPoint3 was = BasePoint(high);
                high.TextHeight = 10;
                Is(P(110.66666666666667, 53), high.TextLocation);
                Is(P(120.66666666666667, 48), high.GetLastVertex(0));
                Is(was.Add(new GeoVector3(-16, 0, 0)), BasePoint(high));

                DxfMLeader scaled = Mirrored(db, "ABC", Right);
                was = BasePoint(scaled);
                scaled.Scale = 2;
                Is(P(100.66666666666667, 52), scaled.TextLocation);
                Is(was.Add(new GeoVector3(-22.666666666666657, 0, 0)), BasePoint(scaled));

                DxfMLeader anew = Mirrored(db, "ABC", Right);
                was = BasePoint(anew);
                DxfMText words = anew.MText;
                words.Contents = "ABCDEF";
                anew.MText = words;
                Is(P(110.66666666666667, 50), anew.TextLocation);
                Is(was.Add(new GeoVector3(-12, 0, 0)), BasePoint(anew));

                DxfMLeader under = Mirrored(db, "ABC", Right);
                under.SetTextAttachmentType(
                    DxfTextAttachmentType.AttachmentBottomOfTopLine,
                    DxfLeaderDirectionType.RightLeader
                );
                Is(P(110.66666666666667, 52.8), under.TextLocation);
            });
        }

        [Fact]
        public void MirroredWordsMetOnBothSides_AreLandedAnewOnTheirRight_AsAutoCadLandsThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 m-b-mxy-*): the words kept and both doglegs moved away from them; their right landed anew.
                DxfMLeader dogleg = Mirrored(db, "ABC", Both);
                dogleg.DoglegLength = 15;
                Is(P(110.66666666666667, 50), dogleg.TextLocation);
                Is(P(83, 48), dogleg.GetLastVertex(0));
                Is(P(127.66666666666667, 48), dogleg.GetLastVertex(1));

                DxfMLeader gap = Mirrored(db, "ABC", Both);
                gap.LandingGap = 5;
                Is(P(113.66666666666667, 50), gap.TextLocation);
                Is(P(90, 48), gap.GetLastVertex(0));
                Is(P(126.66666666666666, 48), gap.GetLastVertex(1));

                DxfMLeader high = Mirrored(db, "ABC", Both);
                high.TextHeight = 10;
                Is(P(126.66666666666666, 53), high.TextLocation);
                Is(P(90, 48), high.GetLastVertex(0));
                Is(P(136.66666666666666, 48), high.GetLastVertex(1));

                DxfMLeader scaled = Mirrored(db, "ABC", Both);
                scaled.Scale = 2;
                Is(P(123.33333333333334, 52), scaled.TextLocation);
                Is(P(82, 48), scaled.GetLastVertex(0));
                Is(P(143.33333333333334, 48), scaled.GetLastVertex(1));

                DxfMLeader anew = Mirrored(db, "ABC", Both);
                DxfMText words = anew.MText;
                words.Contents = "ABCDEF";
                anew.MText = words;
                Is(P(122.66666666666666, 50), anew.TextLocation);
                Is(P(132.66666666666666, 48), anew.GetLastVertex(1));

                DxfMLeader under = Mirrored(db, "ABC", Both);
                under.SetTextAttachmentType(
                    DxfTextAttachmentType.AttachmentBottomOfTopLine,
                    DxfLeaderDirectionType.LeftLeader
                );
                Is(P(110.66666666666667, 52.8), under.TextLocation);
                Is(P(90, 48), under.GetLastVertex(0));
                Is(P(120.66666666666667, 50.8), under.GetLastVertex(1));
            });
        }

        [Fact]
        public void ALeaderAddedOnTheRightOfMirroredWords_IsLandedPastTheirRightEdge_AsAutoCadLandsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 m-mxy-addright, m-mxy-addleft): a new leader from (250, 100) is landed the gap past their right
                // edge, the dogleg from (120.67, 48) against x; one from (0, 100) goes to the leader on their left.
                DxfMLeader right = Mirrored(db, "ABC", Left);
                int line = right.AddLeaderLine(P(250, 100));
                Is(P(120.66666666666667, 48), right.GetLastVertex(line));
                Is(new GeoVector3(-1, 0, 0), right.GetDogleg(right.GetLeaderIndex(line)));
                Is(P(110.66666666666667, 50), right.TextLocation);

                DxfMLeader left = Mirrored(db, "ABC", Left);
                Assert.Equal(0, left.GetLeaderIndex(left.AddLeaderLine(P(0, 100))));
            });
        }

        [Fact]
        public void MirroredWordsReadFromAutoCad_MoveWithTheirDogleg_WithNoFonts_ButNotAsTheyGrow()
        {
            DxfDatabase db = Leaders(null);

            // With no font the words are not measured: a dogleg and a gap move them as AutoCAD does, by no width.
            DxfMLeader dogleg = AutoCads(db, MirroredRecord);
            dogleg.DoglegLength = 15;
            Is(P(117.6666666666666, 50), dogleg.TextLocation);

            DxfMLeader gap = AutoCads(db, MirroredRecord);
            gap.LandingGap = 5;
            Is(P(113.6666666666666, 50), gap.TextLocation);

            // Their height needs their width, which no font gives: they stay, as before.
            DxfMLeader high = AutoCads(db, MirroredRecord);
            high.TextHeight = 10;
            Is(P(110.6666666666666, 50), high.TextLocation);
        }

        // ---- item 2: words of a text style other than the multileader's ------------------------------------------------------------

        /// <summary>The drawing of <see cref="Leaders"/> given the text styles GH of gh.ttf and DEEP of deep.shx.</summary>
        private static DxfDatabase Styled(string folder)
        {
            DxfDatabase db = Leaders(folder);
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH", FileName = "gh.ttf" });
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "DEEP", FileName = "deep.shx" });
            return db;
        }

        /// <summary>A multileader of Standard, added, met at the attachment given on both sides, then given words 4 high at (100, 50) in the text style given, then its lines from the points given.</summary>
        private static DxfMLeader Given(
            DxfDatabase db,
            string words,
            string style,
            int attachment,
            params GeoPoint3[] from
        )
        {
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.LeftLeader);
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.RightLeader);
            mleader.MText = new DxfMText
            {
                Contents = words,
                Location = P(100, 50),
                TextHeight = 4.0,
                TextStyleName = style,
            };

            foreach (GeoPoint3 point in from)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        [Fact]
        public void WordsOfAnotherTextStyle_AreMovedToWhereTheMultileadersFontMeetsThem_AsAutoCadMovesThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Styled(folder);

                // AutoCAD 2021 (D38 s-*, o1..o5-*): "Ag" of Arial given to a multileader of txt.shx stand 0.08 lower, where txt.shx
                // meets them; a leader is landed where txt.shx would land it. Here "Ag" of txt.shx is met 2.67 below its top at the
                // middle of the top line, of gh.ttf 2.57 (its g 200 units of 700 below): 0.095 lower, the leader at 47.33.
                double txt = (4 + (2 * 4.0 / 6)) / 2;
                double gh = (4 + (200 * 4.0 / 700)) / 2;
                DxfMLeader left = Given(db, "Ag", "GH", 1, P(0, 0));
                Is(P(100, 50 + gh - txt), left.TextLocation);
                Is(P(90, 50 - txt), left.GetLastVertex(0));

                DxfMLeader right = Given(db, "Ag", "GH", 1, P(250, 0));
                Is(P(100, 50 + gh - txt), right.TextLocation);
                Is(P(100 + (1080 * 4.0 / 700) + 10, 50 - txt), right.GetLastVertex(0));

                // At the bottom of the bottom line, twice as much (s-TXT-ARIAL-Ag-L-a4); of an SHX font reaching 3 below, 0.33 higher.
                DxfMLeader bottom = Given(db, "Ag", "GH", 4, P(0, 0));
                Is(P(100, 50 + (2 * gh) - (2 * txt)), bottom.TextLocation);
                Is(P(90, 50 - (2 * txt)), bottom.GetLastVertex(0));

                DxfMLeader deep = Given(db, "Ag", "DEEP", 1, P(0, 0));
                Is(P(100, 50 + 3 - txt), deep.TextLocation);
                Is(P(90, 50 - txt), deep.GetLastVertex(0));

                // Given in no drawing, they are moved as the multileader is added (o3-*).
                var memory = new DxfMLeader
                {
                    MText = new DxfMText
                    {
                        Contents = "Ag",
                        Location = P(100, 50),
                        TextHeight = 4.0,
                        TextStyleName = "GH",
                    },
                };
                db.ModelSpace.AppendEntity(memory);
                Is(P(100, 50 + gh - txt), memory.TextLocation);
                memory.AddLeaderLine(P(0, 0));
                Is(P(90, 50 - txt), memory.GetLastVertex(0));

                // Words of the multileader's own style stay where they are given.
                DxfMLeader own = Given(db, "Ag", "Standard", 1, P(0, 0));
                Is(P(100, 50), own.TextLocation);
                Is(P(90, 50 - txt), own.GetLastVertex(0));
            });
        }

        [Fact]
        public void WordsOfAnotherTextStyle_StayWhereTheyAreGiven_WhereNoFontMeasuresThem()
        {
            // With no fonts nothing is measured: the words stay, and the leader is landed 2 below their top, as before.
            DxfDatabase db = Styled(null);
            DxfMLeader left = Given(db, "Ag", "GH", 1, P(0, 0));
            Is(P(100, 50), left.TextLocation);
            Is(P(90, 48), left.GetLastVertex(0));
        }

        // ---- item 3: the leaders run on to the words of a new multileader -----------------------------------------------------------

        [Fact]
        public void ANewMultileader_RunsItsLeadersOnToItsContent_AsAutoCadMakesOne()
        {
            // AutoCAD 2021 (D38 d-*): every new multileader writes 295 1, of words, a block or none, with SetDatabaseDefaults or not.
            DxfDatabase db = Leaders(null);
            DxfMLeader words = Made(db, "ABC", Left);
            Assert.True(words.ExtendLeaderToText);
            Assert.Equal("1", Own(words, 295));

            var block = new DxfBlockTableRecord { Name = "GH_BOX" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
            var boxed = new DxfMLeader();
            db.ModelSpace.AppendEntity(boxed);
            boxed.BlockContent = block;
            boxed.BlockPosition = P(100, 50);
            Assert.True(boxed.ExtendLeaderToText);
            Assert.Equal("1", Own(boxed, 295));

            var none = new DxfMLeader();
            db.ModelSpace.AppendEntity(none);
            Assert.True(none.ExtendLeaderToText);

            // Turned off, it writes 295 0 and bit 30, as AutoCAD 2021 writes it (d-w-ml1-mt1-ext0).
            words.ExtendLeaderToText = false;
            Assert.Equal("0", Own(words, 295));
            Assert.True(Bit(words, 30));
        }

        // ---- item 4: a new leader line of no colour of its own ------------------------------------------------------------------------

        [Fact]
        public void ANewLeaderLine_WritesNoColourOfItsOwn_AsAutoCadWritesOne()
        {
            // AutoCAD 2021 (D38 d-w-ml1-mt1, d-w-ml1-mt1-addleader-line): a LEADER_LINE of no 92, from a point or of a leader.
            DxfDatabase db = Leaders(null);
            DxfMLeader mleader = Made(db, "ABC", Left);
            mleader.AddLeaderLine(mleader.AddLeader());
            mleader.AddLeaderLine(P(250, 0));
            List<List<string>> lines = LeaderLines(mleader);
            Assert.Equal(3, lines.Count);
            Assert.All(lines, l => Assert.DoesNotContain(l, g => g.StartsWith("92 ", StringComparison.Ordinal)));
            Assert.Equal(DxfColor.ByBlock, mleader.GetLeaderLineColor(0));

            // A colour given still writes it, with bit 2 of the line's flags (d-w-ml1-mt1-linered).
            mleader.SetLeaderLineColor(0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
            Assert.Contains("92 -1023410175", LeaderLines(mleader)[0]);
            Assert.Contains("93 2", LeaderLines(mleader)[0]);
        }

        // ---- item 5: bits 15 and 18 of words given, bit 14 of a mirror across YZ ------------------------------------------------

        [Fact]
        public void WordsGiven_SetBit18_AndGiveTheMultileaderTheirColour_AsAutoCadGivesIt()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38 c-o1..o3-*): words given set bit 18; their colour becomes the multileader's text colour (92) and
            // that of the context's MTEXT (90), with bit 15, but for BYBLOCK, which leaves bit 15 and writes 90 BYLAYER.
            DxfMLeader plain = Made(db, "ABC", Left);
            Assert.True(Bit(plain, 18));
            Assert.True(Bit(plain, 15));
            Assert.Equal(DxfColor.ByLayer, plain.TextColor);
            Assert.Equal("-1073741824", Own(plain, 92));
            Assert.Equal("-1073741824", Words(plain, 90));

            var red = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "ABC",
                    Location = P(100, 50),
                    TextHeight = 4.0,
                    Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
                },
            };
            db.ModelSpace.AppendEntity(red);
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), red.TextColor);
            Assert.Equal("-1023410175", Words(red, 90));
            Assert.True(Bit(red, 15));

            // Given anew as read, they keep their colour.
            DxfMText again = red.MText;
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), again.Color);
            again.Contents = "AB";
            red.MText = again;
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), red.TextColor);

            var byBlock = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "ABC",
                    Location = P(100, 50),
                    TextHeight = 4.0,
                    Color = DxfColor.ByBlock,
                },
            };
            db.ModelSpace.AppendEntity(byBlock);
            Assert.Equal(DxfColor.ByBlock, byBlock.TextColor);
            Assert.False(Bit(byBlock, 15));
            Assert.True(Bit(byBlock, 18));
            Assert.Equal("-1073741824", Words(byBlock, 90));
        }

        [Fact]
        public void AMirrorThatTurnsThePlanesXAxisBack_SetsBit14_AndLinesTheContentUpTheOtherWay_AsAutoCadDoes()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);
                var block = new DxfBlockTableRecord { Name = "GH_BOX" };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
                block.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));
                block.AppendEntity(new DxfLine(P(20, 15), P(-10, 15)));
                block.AppendEntity(new DxfLine(P(-10, 15), P(-10, -5)));

                DxfMLeader Boxed()
                {
                    var mleader = new DxfMLeader();
                    db.ModelSpace.AppendEntity(mleader);
                    mleader.BlockContent = block;
                    mleader.BlockPosition = P(100, 50);
                    mleader.AddLeaderLine(P(0, 0));
                    return mleader;
                }

                // AutoCAD 2021 (D38 x-*, a-*): across YZ, and across a plane of normal (1, 0.2, 0), bit 14 and the context's
                // alignment (176) 2 for 0; back across YZ, 0 again, the bit kept; centred, 1 kept; across XY or XZ, and across YZ
                // after a quarter turn, which leave the plane's x axis on its side, neither.
                foreach (Func<DxfMLeader> made in new Func<DxfMLeader>[] { Boxed, () => Made(db, "ABC", Left) })
                {
                    DxfMLeader yz = made();
                    yz.TransformBy(Mirror(1, 0, 0));
                    Assert.True(Bit(yz, 14));
                    Assert.Equal("2", Head(yz, 176));

                    DxfMLeader nx = made();
                    nx.TransformBy(Mirror(1, 0.2, 0));
                    Assert.True(Bit(nx, 14));
                    Assert.Equal("2", Head(nx, 176));

                    DxfMLeader twice = made();
                    twice.TransformBy(Mirror(1, 0, 0));
                    twice.TransformBy(Mirror(1, 0, 0));
                    Assert.True(Bit(twice, 14));
                    Assert.Equal("0", Head(twice, 176));

                    DxfMLeader centred = made();
                    centred.TextAlignmentType = DxfTextAlignmentType.CenterAlignment;
                    centred.TransformBy(Mirror(1, 0, 0));
                    Assert.Equal("1", Head(centred, 176));

                    foreach (
                        GeoTransform3[] keeps in new[]
                        {
                            new[] { Mirror(0, 0, 1) },
                            new[] { Mirror(0, 1, 0) },
                            new[] { GeoTransform3.RotationZ(Math.PI / 2), Mirror(1, 0, 0) },
                        }
                    )
                    {
                        DxfMLeader kept = made();

                        foreach (GeoTransform3 transform in keeps)
                        {
                            kept.TransformBy(transform);
                        }

                        Assert.False(Bit(kept, 14));
                        Assert.Equal("0", Head(kept, 176));
                    }
                }
            });
        }
    }
}
