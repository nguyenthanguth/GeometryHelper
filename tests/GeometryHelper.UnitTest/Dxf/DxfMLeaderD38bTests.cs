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
    /// The multileader items D38 left, as AutoCAD 2021 answered them (plugin D38b, headless): the base point of the content of
    /// words given (10 of the context data), the plane of words turned in code, words mirrored across a plane that turns them
    /// upside down, and the small readings: a line's colour of BYBLOCK, the alignment of the context, 294 of a mirrored block,
    /// 295 before R2013 and a multileader of no content. Fonts are those of F1b's tests, written in code: txt.shx's capitals
    /// inked 4 by 6 and moving the pen 6, its g reaching 2 below the baseline, so "ABC" 4 high is 10.67 wide.
    /// </summary>
    public class DxfMLeaderD38bTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        // ---- the fonts, as F1b's tests write them ---------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down from the origin up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The bytes of a unifont of AutoCAD's form, 6 above and 2 below as txt.shx is: a space, the capitals and g, its g reaching 2 below the baseline.</summary>
        private static byte[] Txt()
        {
            byte[] g = { 1, 0x2C, 0x40, 0x64, 2, 0x4C, 0x20, 0 };
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

        /// <summary>A folder of its own holding txt.shx (<see cref="Txt"/>); deleted after the test.</summary>
        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-d38b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Txt());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // ---- the drawings -----------------------------------------------------------------------------------------------------------

        private static GeoPoint3 P(double x, double y, double z = 0) => new GeoPoint3(x, y, z);

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

        /// <summary>A multileader of no content added to the drawing, of a gap of 2 and doglegs of 8, met at the attachment given on both sides, as D38b's plugin made AutoCAD's.</summary>
        private static DxfMLeader Empty(DxfDatabase db, int attachment = 1)
        {
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.LandingGap = 2;
            mleader.DoglegLength = 8;
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.LeftLeader);
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.RightLeader);
            return mleader;
        }

        /// <summary>An <see cref="Empty"/> multileader given words 4 high at (100, 50), held and turned as given, then its lines from the points given.</summary>
        private static DxfMLeader Given(
            DxfDatabase db,
            string words,
            int attachment,
            DxfAttachmentPoint held,
            double rotation,
            params GeoPoint3[] from
        )
        {
            DxfMLeader mleader = Empty(db, attachment);
            mleader.MText = new DxfMText
            {
                Contents = words,
                Location = P(100, 50),
                TextHeight = 4.0,
                Attachment = held,
                Rotation = rotation,
            };

            foreach (GeoPoint3 point in from)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        private static DxfMLeader Given(DxfDatabase db, string words, params GeoPoint3[] from) =>
            Given(db, words, 1, DxfAttachmentPoint.TopLeft, 0.0, from);

        private static List<DxfTypedValue> Tail(DxfEntity entity) => entity.Item.File.TailOf(entity.Item, 0);

        /// <summary>The value of a group after the context data; null where there is none.</summary>
        private static string Own(DxfMLeader mleader, int code)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int end = tail.FindIndex(g => g.TypeCode == 301);
            return tail.Skip(end + 1).Where(g => g.TypeCode == code).Select(g => g.Value.Trim()).FirstOrDefault();
        }

        /// <summary>A point of the head of the context data, before its MTEXT, block and leaders, by its x code (110, 111, 112).</summary>
        private static GeoVector3 Axis(DxfMLeader mleader, int code)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int at = tail.FindIndex(g => g.TypeCode == code);
            return new GeoVector3(Num(tail[at].Value), Num(tail[at + 1].Value), Num(tail[at + 2].Value));
        }

        /// <summary>The base point of the content, 10 of the context data.</summary>
        private static GeoPoint3 BasePoint(DxfMLeader mleader)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int start = tail.FindIndex(g => g.TypeCode == 300);
            int at = tail.FindIndex(start, g => g.TypeCode == 10);
            return new GeoPoint3(Num(tail[at].Value), Num(tail[at + 1].Value), Num(tail[at + 2].Value));
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

        // ---- item 1: the base point of words given ----------------------------------------------------------------------------------

        [Fact]
        public void WordsGiven_PutTheBasePointWhereALeaderFromTheLeftWouldMeetThem_AsAutoCadPutsIt()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38b b-*, d-w-*): the gap short of where they are given, as far below as the left is met; no font is
            // needed for that.
            Is(P(98, 48), BasePoint(Given(db, "ABC")));
            Is(P(98, 50), BasePoint(Given(db, "ABC", 0, DxfAttachmentPoint.TopLeft, 0.0)));
            Is(P(98, 50 - (26.0 / 3)), BasePoint(Given(db, "A\\PB\\PC", 2, DxfAttachmentPoint.TopLeft, 0.0)));
            Is(P(98, 50 - (52.0 / 3)), BasePoint(Given(db, "A\\PB\\PC", 4, DxfAttachmentPoint.TopLeft, 0.0)));

            // Held at their top centre, the base point is the same; with no font to tell their width, they stay where given.
            DxfMLeader centred = Given(db, "ABC", 1, DxfAttachmentPoint.TopCenter, 0.0);
            Is(P(98, 48), BasePoint(centred));
            Is(P(100, 50), centred.TextLocation);

            // Given anew with no leader, the base point goes with them (b-anew-N).
            DxfMLeader anew = Given(db, "ABC");
            DxfMText words = anew.MText;
            words.Contents = "ABCDEF";
            words.Location = P(150, 80);
            anew.MText = words;
            Is(P(148, 78), BasePoint(anew));
            Is(P(150, 80), anew.TextLocation);

            // Given in no drawing, then added; a leader from the left still ends its dogleg there.
            var memory = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "ABC",
                    Location = P(100, 50),
                    TextHeight = 4.0,
                },
            };
            db.ModelSpace.AppendEntity(memory);
            Is(P(98, 48), BasePoint(memory));
            memory.AddLeaderLine(P(0, 0));
            Is(P(90, 48), memory.GetLastVertex(0));
        }

        [Fact]
        public void WordsGivenWhereTheFontsMeasureThem_PutTheBasePointAndTheirLeftWhereAutoCadPutsThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38b b-att*-Ag-N): "Ag", its g 1.33 below the baseline, met 2.67 below its top.
                Is(P(98, 50 - (8.0 / 3)), BasePoint(Given(db, "Ag")));

                // Held at their top centre or right, their left edge goes where they are given (b-held2-N, b-held3-N).
                DxfMLeader centred = Given(db, "ABC", 1, DxfAttachmentPoint.TopCenter, 0.0);
                Is(P(105.33333333333333, 50), centred.TextLocation);
                Is(P(98, 48), BasePoint(centred));

                DxfMLeader right = Given(db, "ABC", 1, DxfAttachmentPoint.TopRight, 0.0);
                Is(P(110.66666666666667, 50), right.TextLocation);
                Is(P(98, 48), BasePoint(right));

                // Given in no drawing, the base point is put again by the fonts as the multileader is added.
                var memory = new DxfMLeader
                {
                    MText = new DxfMText
                    {
                        Contents = "Ag",
                        Location = P(100, 50),
                        TextHeight = 4.0,
                    },
                };
                db.ModelSpace.AppendEntity(memory);
                Is(P(98, 50 - (8.0 / 3)), BasePoint(memory));
            });
        }

        // ---- item 2: words turned in code -----------------------------------------------------------------------------------------

        [Fact]
        public void WordsTurnedInCode_TurnThePlaneWithThem_AsAutoCadTurnsIt()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38 m-rot180, D38b r-*): the plane's x axis along the words, its y axis across, the base point where a
            // leader from the left of that plane would meet them; a leader from (250, 0) is on that left.
            DxfMLeader half = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI);
            Is(new GeoVector3(-1, 0, 0), Axis(half, 111));
            Is(new GeoVector3(0, -1, 0), Axis(half, 112));
            Is(P(102, 52), BasePoint(half));
            int line = half.AddLeaderLine(P(250, 0));
            Is(P(110, 52), half.GetLastVertex(line));
            Is(new GeoVector3(-1, 0, 0), half.GetDogleg(half.GetLeaderIndex(line)));
            Is(P(100, 50), half.TextLocation);

            DxfMLeader backward = Empty(db);
            backward.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
                Direction = new GeoVector3(-1, 0, 0),
            };
            Is(new GeoVector3(-1, 0, 0), Axis(backward, 111));
            Is(P(102, 52), BasePoint(backward));

            // A turn of 30 degrees or a quarter turns it as far (r-rot30-L, r-rot90-R).
            DxfMLeader thirty = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI / 6, P(0, 0));
            Is(new GeoVector3(Math.Cos(Math.PI / 6), 0.5, 0), Axis(thirty, 111));
            Is(P(99.267949192431123, 47.267949192431123), BasePoint(thirty));
            Is(P(92.339745962155604, 43.267949192431125), thirty.GetLastVertex(0));

            DxfMLeader quarter = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI / 2, P(250, 0));
            Is(P(102, 40), quarter.GetLastVertex(0));
            Is(new GeoVector3(0, 1, 0), quarter.GetDogleg(0));
        }

        [Fact]
        public void WordsTurnedHalfRoundAndMetOnTheirRight_AreMovedAsAutoCadMovesThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 m-rot180-*): a leader from (0, 0) meets them on the right of the turned plane, its dogleg from
                // (79.33, 52); a dogleg of 15 moves them to (107, 50), a gap of 5 to (97, 50) and the leader to (73.33, 52).
                DxfMLeader turned = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI, P(0, 0));
                Is(P(79.333333333333329, 52), turned.GetLastVertex(0));
                Is(new GeoVector3(1, 0, 0), turned.GetDogleg(0));

                DxfMLeader dogleg = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI, P(0, 0));
                dogleg.DoglegLength = 15;
                Is(P(107, 50), dogleg.TextLocation);

                DxfMLeader gap = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI, P(0, 0));
                gap.LandingGap = 5;
                Is(P(97, 50), gap.TextLocation);
                Is(P(73.333333333333343, 52), gap.GetLastVertex(0));
            });
        }

        // ---- item 3: words mirrored upside down -------------------------------------------------------------------------------------

        [Fact]
        public void WordsMirroredAcrossXZ_AreTurnedUpright_AsAutoCadTurnsThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 x-w-xz, D38b z-xz-*): still along x, met again by the mirrored leader at their attachment: their
                // top at -46 for a leader at -48, the plane kept and 294 of nought.
                DxfMLeader across = Given(db, "ABC", P(0, 0));
                across.TransformBy(Mirror(0, 1, 0));
                Is(P(100, -46), across.TextLocation);
                Is(new GeoVector3(1, 0, 0), across.MText.Direction);
                Is(new GeoVector3(0, 0, 1), across.MText.Normal);
                Is(P(90, -48), across.GetLastVertex(0));
                Is(P(98, -48), BasePoint(across));
                Is(new GeoVector3(0, 1, 0), Axis(across, 112));
                Assert.Equal("0", Own(across, 294));

                DxfMLeader deep = Given(db, "Ag", P(0, 0));
                deep.TransformBy(Mirror(0, 1, 0));
                Is(P(100, -44.666666666666671), deep.TextLocation);

                DxfMLeader top = Given(db, "ABC", 0, DxfAttachmentPoint.TopLeft, 0.0, P(0, 0));
                top.TransformBy(Mirror(0, 1, 0));
                Is(P(100, -50), top.TextLocation);

                DxfMLeader bottom = Given(db, "ABC", 4, DxfAttachmentPoint.TopLeft, 0.0, P(0, 0));
                bottom.TransformBy(Mirror(0, 1, 0));
                Is(P(100, -42), bottom.TextLocation);

                DxfMLeader centred = Given(db, "ABC", 1, DxfAttachmentPoint.TopCenter, 0.0, P(0, 0));
                centred.TransformBy(Mirror(0, 1, 0));
                Is(P(105.33333333333333, -46), centred.TextLocation);

                // Across a plane of normal (0.2, 1, 0) (x-w-ny), and one of (0.6, 0.1, 0.8) whose turn AutoCAD reads in the words'
                // own system (z-n060108-L).
                DxfMLeader near = Given(db, "ABC", P(0, 0));
                near.TransformBy(Mirror(0.2, 1, 0));
                Is(P(74.615384615384627, -80.923076923076934), near.TextLocation);
                Is(new GeoVector3(0.92307692307692302, -0.38461538461538458, 0), near.MText.Direction);

                DxfMLeader tilted = Given(db, "ABC", P(0, 0));
                tilted.TransformBy(Mirror(0.6, 0.1, 0.8));
                Is(P(23.24752475247525, 33.207920792079207, -102.3366336633663), tilted.TextLocation);
                Is(
                    new GeoVector3(0.28712871287128722, -0.11881188118811883, -0.95049504950495045),
                    tilted.MText.Direction
                );

                // Turned in code first, the way kept is the one nearer the turn they had: a quarter turn mirrored across XY runs
                // up y still, facing down (q-rot90-xy-L); half round, it runs along x (r-rot180-mxy-L).
                DxfMLeader quarter = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI / 2, P(0, 0));
                quarter.TransformBy(Mirror(0, 0, 1));
                Is(P(104, 50), quarter.TextLocation);
                Is(new GeoVector3(0, 1, 0), quarter.MText.Direction);
                Is(new GeoVector3(0, 0, -1), quarter.MText.Normal);

                DxfMLeader half = Given(db, "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI, P(0, 0));
                half.TransformBy(Mirror(0, 0, 1));
                Is(P(89.333333333333329, 50), half.TextLocation);
                Is(new GeoVector3(1, 0, 0), half.MText.Direction);
                Assert.Equal("1", Own(half, 294));

                // Across XY and YZ, as before (D38 x-w-xy, x-w-yz).
                DxfMLeader xy = Given(db, "ABC", P(0, 0));
                xy.TransformBy(Mirror(0, 0, 1));
                Is(P(110.66666666666667, 50), xy.TextLocation);
                Is(new GeoVector3(-1, 0, 0), xy.MText.Direction);

                DxfMLeader yz = Given(db, "ABC", P(0, 0));
                yz.TransformBy(Mirror(1, 0, 0));
                Is(P(-100, 50), yz.TextLocation);
                Is(new GeoVector3(1, 0, 0), yz.MText.Direction);
            });
        }

        // ---- item 4: the small readings ---------------------------------------------------------------------------------------------

        [Fact]
        public void ALineGivenByBlock_WritesItsFlagAndNoColour_AndALineOfNoColourReadsByBlock_AsAutoCadDoes()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38 d-w-ml1-mt1-lineblock, c-lineblock-green): 93 2 and no 92.
            DxfMLeader block = Given(db, "ABC", P(0, 0));
            block.SetLeaderLineColor(0, DxfColor.ByBlock);
            Assert.Contains("93 2", LeaderLines(block)[0]);
            Assert.DoesNotContain(LeaderLines(block)[0], g => g.StartsWith("92 ", StringComparison.Ordinal));
            Assert.Equal(DxfColor.ByBlock, block.GetLeaderLineColor(0));

            // A line of no colour of its own reads BYBLOCK on a multileader of green lines (c-mlcolor-newline).
            DxfMLeader green = Given(db, "ABC", P(0, 0));
            green.LeaderLineColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            green.AddLeaderLine(P(0, 100));
            Assert.Equal(DxfColor.ByBlock, green.GetLeaderLineColor(0));
            Assert.Equal(DxfColor.ByBlock, green.GetLeaderLineColor(1));

            // Given BYBLOCK there, it is flagged so, with no 92 (c-lineblock-green).
            green.SetLeaderLineColor(0, DxfColor.ByBlock);
            Assert.Contains("93 2", LeaderLines(green)[0]);
            Assert.DoesNotContain(LeaderLines(green)[0], g => g.StartsWith("92 ", StringComparison.Ordinal));
        }

        [Fact]
        public void TheAlignment_IsReadFromTheContext_AndAMirroredBlockSays294_AsAutoCadReadsAndWritesThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D38 x-w-yz, a-w-right-yz): mirrored across YZ, the words read lined up on the right, as the
                // context's 176 says; lined up on the right and mirrored, on the left.
                DxfMLeader yz = Given(db, "ABC", P(0, 0));
                yz.TransformBy(Mirror(1, 0, 0));
                Assert.Equal(DxfTextAlignmentType.RightAlignment, yz.TextAlignmentType);

                DxfMLeader right = Given(db, "ABC", P(0, 0));
                right.TextAlignmentType = DxfTextAlignmentType.RightAlignment;
                right.TransformBy(Mirror(1, 0, 0));
                Assert.Equal(DxfTextAlignmentType.LeftAlignment, right.TextAlignmentType);

                // A block whose plane's x axis a mirror turns back says so (294 of 1, x-k-yz, x-k-nx); back again, not (x-k-yz-yz);
                // across XY, not (x-k-xy).
                var box = new DxfBlockTableRecord { Name = "GH_BOX" };
                db.BlockTable.Add(box);
                box.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
                box.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));

                DxfMLeader Boxed()
                {
                    var mleader = new DxfMLeader();
                    db.ModelSpace.AppendEntity(mleader);
                    mleader.BlockContent = box;
                    mleader.BlockPosition = P(100, 50);
                    mleader.AddLeaderLine(P(0, 0));
                    return mleader;
                }

                DxfMLeader once = Boxed();
                once.TransformBy(Mirror(1, 0, 0));
                Assert.Equal("1", Own(once, 294));
                Assert.Equal(DxfTextAlignmentType.RightAlignment, once.TextAlignmentType);

                DxfMLeader slanted = Boxed();
                slanted.TransformBy(Mirror(1, 0.2, 0));
                Assert.Equal("1", Own(slanted, 294));

                DxfMLeader twice = Boxed();
                twice.TransformBy(Mirror(1, 0, 0));
                twice.TransformBy(Mirror(1, 0, 0));
                Assert.Equal("0", Own(twice, 294));

                DxfMLeader xy = Boxed();
                xy.TransformBy(Mirror(0, 0, 1));
                Assert.Equal("0", Own(xy, 294));
            });
        }

        [Fact]
        public void ANewMultileaderBeforeR2013_WritesNo295_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 (D38 e-w-2010, e-k-2010): no 295 in R2010; 295 1 from R2013.
            string text = DxfHatchLeaderDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1024\n");
            Assert.NotEqual(DxfHatchLeaderDrawings.R2018, text);
            DxfDatabase old = DxfDatabase.Parse(text);
            var mleader = new DxfMLeader();
            old.ModelSpace.AppendEntity(mleader);
            mleader.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            Assert.Null(Own(mleader, 295));
            Assert.Null(
                Own(
                    DxfDatabase
                        .Parse(old.ToText())
                        .ModelSpace.OfType<DxfMLeader>()
                        .Single(m => m.Handle == mleader.Handle),
                    295
                )
            );

            var now = new DxfMLeader();
            Leaders(null).ModelSpace.AppendEntity(now);
            Assert.Equal("1", Own(now, 295));
        }

        [Fact]
        public void AMultileaderOfNoContent_IsOfWords_AndItsFirstLineIsLandedAtItsPoint_AsAutoCadMakesIt()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38 d-none-ml1, D38b n-none-*): of words (172 of 2) and none held; a line from (30, 40) is landed
            // there, its dogleg towards the base point, the line of the one vertex; a second line from (60, 10) joins it.
            DxfMLeader none = Empty(db);
            Assert.Equal(DxfContentType.MTextContent, none.ContentType);
            Assert.False(none.HasContent());
            Assert.Null(none.MText);
            Assert.Equal("2", Own(none, 172));

            int first = none.AddLeaderLine(P(30, 40));
            Assert.Equal(1, none.VerticesCount(first));
            Is(P(30, 40), none.GetFirstVertex(first));
            Is(new GeoVector3(-1, 0, 0), none.GetDogleg(none.GetLeaderIndex(first)));

            int second = none.AddLeaderLine(P(60, 10));
            Assert.Equal(none.GetLeaderIndex(first), none.GetLeaderIndex(second));
            Assert.Equal(2, none.VerticesCount(second));
            Is(P(30, 40), none.GetLastVertex(second));

            // From the base point itself, the dogleg runs along x (d-none-ml1-line).
            DxfMLeader at = Empty(db);
            int line = at.AddLeaderLine(P(0, 0));
            Assert.Equal(1, at.VerticesCount(line));
            Is(new GeoVector3(1, 0, 0), at.GetDogleg(0));
        }

        // ---- past the brief: what AutoCAD 2021 showed of the same rules --------------------------------------------------------------

        [Fact]
        public void WordsGivenAfterTheirLeaders_AreMovedToMeetTheFirst_AsAutoCadMovesThem()
        {
            // AutoCAD 2021 (D38b b-o4-L, n-none-words): a line from (0, 0) given first, then the words: they meet its dogleg's end
            // at (8, 0) on their left, the base point there.
            DxfDatabase db = Leaders(null);
            DxfMLeader left = Empty(db);
            left.AddLeaderLine(P(0, 0));
            left.MText = new DxfMText
            {
                Contents = "ABC",
                Location = P(100, 50),
                TextHeight = 4.0,
            };
            Is(P(10, 2), left.TextLocation);
            Is(P(8, 0), BasePoint(left));

            // On their right (b-o4-R), where the fonts tell how wide they are.
            InFolder(folder =>
            {
                DxfMLeader right = Empty(Leaders(folder));
                right.AddLeaderLine(P(250, 0));
                right.MText = new DxfMText
                {
                    Contents = "ABC",
                    Location = P(100, 50),
                    TextHeight = 4.0,
                };
                Is(P(229.33333333333334, 2), right.TextLocation);
                Is(P(227.33333333333334, 0), BasePoint(right));
            });
        }

        [Fact]
        public void WordsOfNoLeader_MeetTheBasePointAgainAsTheyAreScaledOrAttachedAnew_AsAutoCadMovesThem()
        {
            DxfDatabase db = Leaders(null);

            // AutoCAD 2021 (D38b b-scale2-N, b-att4set-N): at a scale of 2 they stand at (102, 52); met at the bottom line on the
            // left, at (100, 52.8); the base point stays at (98, 48). Made 10 high, they stay (b-th10-N).
            DxfMLeader scaled = Given(db, "ABC");
            scaled.Scale = 2;
            Is(P(102, 52), scaled.TextLocation);
            Is(P(98, 48), BasePoint(scaled));

            DxfMLeader attached = Given(db, "ABC");
            attached.SetTextAttachmentType(
                DxfTextAttachmentType.AttachmentBottomLine,
                DxfLeaderDirectionType.LeftLeader
            );
            Is(P(100, 52.8), attached.TextLocation);
            Is(P(98, 48), BasePoint(attached));

            DxfMLeader high = Given(db, "ABC");
            high.TextHeight = 10;
            Is(P(100, 50), high.TextLocation);
        }

        [Fact]
        public void WordsMetOnlyOnTheirRight_PutTheBasePointAnewAsTheLeftIsAttachedAnew_AsAutoCadPutsIt()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021 (D38b r-rot180-att4-L): turned half round and met on the right of their plane, the left set to the
                // bottom line moves the base point to (102, 54.8), the words kept.
                DxfMLeader turned = Given(Leaders(folder), "ABC", 1, DxfAttachmentPoint.TopLeft, Math.PI, P(0, 0));
                turned.SetTextAttachmentType(
                    DxfTextAttachmentType.AttachmentBottomLine,
                    DxfLeaderDirectionType.LeftLeader
                );
                Is(P(100, 50), turned.TextLocation);
                Is(P(102, 54.8), BasePoint(turned));
            });
        }

        [Fact]
        public void WordsWhoseInkReachesBelowTheirBaseline_AreMetUnderTheirDepthAtAttachment8_AsAutoCadMeetsThem()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021 (D38b b-att8-Ag-N): "Ag" met at the underline of its lines, 5.33 below its top, for 4.8 where it
                // has no depth (b-att8-3l-N).
                Is(P(98, 50 - (16.0 / 3)), BasePoint(Given(Leaders(folder), "Ag", 8, DxfAttachmentPoint.TopLeft, 0.0)));
                Is(P(98, 45.2), BasePoint(Given(Leaders(folder), "ABC", 8, DxfAttachmentPoint.TopLeft, 0.0)));
            });
        }
    }
}
