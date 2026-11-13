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
    /// A solid fill, a multileader and an angular dimension saved as R12 (AC1009) as AutoCAD 2021 writes them, found by probing
    /// its console (Database.DxfOut of side databases at R12, the same drawings in R2018 read by this library): a fill as the
    /// SOLIDs of horizontal strips in a plane turned over (the normal (0, 0, -1), x negated) and the LINEs of the strips'
    /// sides that are not left out; a multileader's frame round the ink of its words, the dogleg that runs on to the words held
    /// at the middle or the right; the words of an angular dimension one TEXT of AutoCAD's own form. The bodies are built here
    /// in code.
    /// </summary>
    public class DxfSaveR12FillTests
    {
        private const double Near = 1E-6;

        private static DxfDatabase Drawing() => new DxfDatabase("AC1032");

        private static DxfDatabase Ezdxf() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

        private static string R12(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                return Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }
        }

        private sealed class Record
        {
            internal Record(string type)
            {
                Type = type;
                Groups = new List<KeyValuePair<int, string>>();
            }

            internal string Type { get; }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal string First(int code) => Groups.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();

            internal double Number(int code) => double.Parse(First(code), CultureInfo.InvariantCulture);

            internal bool Has(int code) => Groups.Any(g => g.Key == code);
        }

        /// <summary>The records of the BLOCKS and ENTITIES sections: a record runs from its group 0 to the next.</summary>
        private static Dictionary<string, List<Record>> Sections(string text)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var sections = new Dictionary<string, List<Record>>();
            List<Record> current = null;
            Record record = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    sections[lines[i + 3]] = current = new List<Record>();
                    record = null;
                    i += 2;
                    continue;
                }

                if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    current = null;
                    record = null;
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (code == 0)
                {
                    record = new Record(value);
                    current.Add(record);
                    continue;
                }

                record?.Groups.Add(new KeyValuePair<int, string>(code, value));
            }

            return sections;
        }

        /// <summary>What the INSERT standing for an entity holds: the records of its block, from the one after its BLOCK to the ENDBLK.</summary>
        private static List<Record> PiecesOf(Dictionary<string, List<Record>> sections, string handle)
        {
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == handle);
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            Assert.True(start >= 0, "no block " + insert.First(2));
            return blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
        }

        private static GeoPoint2[] Rectangle(double x0, double y0, double x1, double y1) =>
            new[] { new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1) };

        /// <summary>A HATCH filled solid whose loops are polygons, the first outside, the rest holes (or islands where they lie in a hole).</summary>
        private static DxfHatch Fill(DxfDatabase database, params GeoPoint2[][] loops)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");

            for (int i = 0; i < loops.Length; i++)
            {
                hatch.AppendLoop(
                    i == 0 ? DxfHatchLoopTypes.External : DxfHatchLoopTypes.Default,
                    loops[i],
                    new double[loops[i].Length]
                );
            }

            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        /// <summary>A SOLID of the fill as its corners in the world: x negated back, as the plane turned over has them.</summary>
        private static double[] Corners(Record solid) =>
            new[] { 10, 20, 11, 21, 12, 22, 13, 23 }
                .Select((c, i) => i % 2 == 0 ? -solid.Number(c) : solid.Number(c))
                .ToArray();

        private static string Line(Record line) =>
            string.Join(
                ",",
                new[] { 10, 20, 11, 21 }.Select(c =>
                    Math.Round(line.Number(c), 4).ToString(CultureInfo.InvariantCulture)
                )
            );

        // ---- a solid fill -------------------------------------------------------------------------------------------------------

        [Fact]
        public void ARectangle100By60FilledSolid_IsTwoSolidsInThePlaneTurnedOver_AndTheLinesOfItsTwoSides()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Rectangle(0, 0, 100, 60));

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);

            Assert.Equal(new[] { "SOLID", "SOLID", "LINE", "LINE" }, pieces.Select(r => r.Type));
            Assert.All(pieces.Take(2), r => Assert.Equal(-1.0, r.Number(230), 9));

            // The first triangle is the top left, the bottom right and the bottom left, the second the top left, the top right and the
            // bottom right: the third corner written again as the fourth.
            Assert.Equal(new[] { 0.0, 60.0, 100.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, Corners(pieces[0]).Select(Zero));
            Assert.Equal(new[] { 0.0, 60.0, 100.0, 60.0, 100.0, 0.0, 100.0, 0.0 }, Corners(pieces[1]).Select(Zero));
            Assert.Equal("0,0,0,60", Line(pieces[2]));
            Assert.Equal("100,60,100,0", Line(pieces[3]));
        }

        private static double Zero(double value) => Math.Abs(value) < Near ? 0.0 : value;

        [Fact]
        public void ARectangleWithARectangularHole_IsEightSolidsAndEightLines_ThePieceOnTheRightBeforeThePieceOnTheLeft()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Rectangle(0, 0, 100, 60), Rectangle(30, 20, 70, 40));

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);
            Record[] solids = pieces.Where(r => r.Type == "SOLID").ToArray();
            string[] lines = pieces.Where(r => r.Type == "LINE").Select(Line).ToArray();

            Assert.Equal(8, solids.Length);
            Assert.Equal(8, lines.Length);

            // Strips from the top down; of the middle one, between the hole and the sides, the piece 70 to 100 before 0 to 30.
            Assert.Equal(new[] { 60.0, 60.0, 40.0, 40.0, 40.0, 40.0, 20.0, 20.0 }, solids.Select(s => Corners(s)[1]));
            Assert.Equal(70.0, Corners(solids[2])[0], 6);
            Assert.Equal(0.0, Corners(solids[4])[0], 6);
            Assert.Equal(
                new[]
                {
                    "0,40,0,60",
                    "100,60,100,40",
                    "70,20,70,40",
                    "100,40,100,20",
                    "0,20,0,40",
                    "30,40,30,20",
                    "0,0,0,20",
                    "100,20,100,0",
                },
                lines
            );
        }

        [Fact]
        public void ATriangleFilledSolid_IsOneSolidAndTheLinesOfItsTwoSlantedSides_TheRightSideFirst()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(
                database,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(50, 80) }
            );

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);

            Assert.Equal(new[] { "SOLID", "LINE", "LINE" }, pieces.Select(r => r.Type));
            Assert.Equal(new[] { 50.0, 80.0, 100.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, Corners(pieces[0]).Select(Zero));
            Assert.Equal("50,80,100,0", Line(pieces[1]));
            Assert.Equal("0,0,50,80", Line(pieces[2]));
        }

        [Fact]
        public void AFillWithASharpNotchInIt_LeavesOutTheSlantedSidesOfTheNotch_ButNotThoseOfARoundedOne()
        {
            // 100 by 60 with a V cut from the top down to (50, 20): two triangles over a rectangle, the notch's sides not drawn.
            DxfDatabase sharp = Drawing();
            DxfHatch notch = Fill(
                sharp,
                new[]
                {
                    new GeoPoint2(0, 0),
                    new GeoPoint2(100, 0),
                    new GeoPoint2(100, 60),
                    new GeoPoint2(50, 20),
                    new GeoPoint2(0, 60),
                }
            );
            List<Record> pieces = PiecesOf(Sections(R12(sharp)), notch.Handle);
            Assert.Equal(4, pieces.Count(r => r.Type == "SOLID"));
            Assert.Equal(
                new[] { "0,20,0,60", "100,60,100,20", "0,0,0,20", "100,20,100,0" },
                pieces.Where(r => r.Type == "LINE").Select(Line)
            );

            // With a flat bottom the notch's sides are drawn.
            DxfDatabase flat = Drawing();
            DxfHatch trough = Fill(
                flat,
                new[]
                {
                    new GeoPoint2(0, 0),
                    new GeoPoint2(100, 0),
                    new GeoPoint2(100, 60),
                    new GeoPoint2(60, 20),
                    new GeoPoint2(40, 20),
                    new GeoPoint2(0, 60),
                }
            );
            string[] lines = PiecesOf(Sections(R12(flat)), trough.Handle)
                .Where(r => r.Type == "LINE")
                .Select(Line)
                .ToArray();
            Assert.Contains("0,60,40,20", lines);
            Assert.Contains("60,20,100,60", lines);
        }

        [Fact]
        public void AGradientFill_IsTheSolidsOfItsStrips_WithNoLines()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Rectangle(0, 0, 100, 60));
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);

            Assert.Equal(new[] { "SOLID", "SOLID" }, pieces.Select(r => r.Type));
        }

        [Fact]
        public void AFillAtAnElevationOfFive_StandsFiveHigh_ItsSolidsFiveBelowInThePlaneTurnedOver()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Rectangle(0, 0, 100, 60));
            hatch.Elevation = 5.0;

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);

            Assert.All(pieces.Where(r => r.Type == "SOLID"), r => Assert.Equal(-5.0, r.Number(30), 9));
            Assert.All(pieces.Where(r => r.Type == "LINE"), r => Assert.Equal(5.0, r.Number(30), 9));
        }

        [Fact]
        public void TwoSeparateLoops_AreCutTogether_TheBoxOnTheRightBeforeTheOneOnTheLeft()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Rectangle(0, 0, 100, 60), Rectangle(150, 0, 250, 60));

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);
            Record[] solids = pieces.Where(r => r.Type == "SOLID").ToArray();

            Assert.Equal(4, solids.Length);
            Assert.Equal(150.0, Corners(solids[0])[0], 6);
            Assert.Equal(0.0, Corners(solids[2])[0], 6);
        }

        [Fact]
        public void ASlantedPieceThatTwoLevelsCut_IsTwoTrapezoids_AsALeaningSideCannotBeJoined()
        {
            // A leaning arch: the left foot is cut where the right foot's corner stands, though the same two edges bound it.
            double turn = 0.17;
            GeoPoint2 R(double x, double y) =>
                new GeoPoint2(x * Math.Cos(turn) - y * Math.Sin(turn), x * Math.Sin(turn) + y * Math.Cos(turn));
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, new[] { R(0, 0), R(50, 40), R(100, 0), R(100, 60), R(0, 60) });

            List<Record> pieces = PiecesOf(Sections(R12(database)), hatch.Handle);

            Assert.Equal(7, pieces.Count(r => r.Type == "SOLID"));
        }

        // ---- a multileader ------------------------------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down where it starts, up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The bytes of a unifont of AutoCAD's form holding the capitals and the space: each capital 6 wide, 4 high at 4 high letters.</summary>
        private static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, byte[]> { { ' ', new byte[] { 2, 0x60, 0 } }, { '?', Capital } };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters)
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

        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-r12-fill-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A multileader of two lines of words held at the middle left, its leader line from (0, 0), in a drawing that has fonts.</summary>
        private static DxfMLeader Leader(DxfDatabase database, bool frame, DxfAttachmentPoint attachment)
        {
            var leader = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "ABC\\PDEFGH",
                    Location = new GeoPoint3(30, 20, 0),
                    TextHeight = 4,
                    Attachment = attachment,
                },
                EnableFrameText = frame,
            };
            database.ModelSpace.AppendEntity(leader);
            leader.AddLeaderLine(new GeoPoint3(0, 0, 0));
            return leader;
        }

        [Fact]
        public void TheWordsOfAMultileaderWithAFrame_HaveItDrawnLast_AFivePointPolylineRoundTheirInk()
        {
            InFolder(folder =>
            {
                DxfDatabase database = Ezdxf();
                database.FontSearchPath = folder;
                DxfMLeader leader = Leader(database, true, DxfAttachmentPoint.TopLeft);
                DxfMText words = leader.Explode().OfType<DxfMText>().Single();
                double gap = 2.0;

                List<Record> pieces = PiecesOf(Sections(R12(database)), leader.Handle);

                int last = pieces.FindLastIndex(r => r.Type == "POLYLINE");
                Record[] vertices = pieces.Skip(last + 1).TakeWhile(r => r.Type == "VERTEX").ToArray();
                Assert.Equal(5, vertices.Length);
                Assert.Equal("0", pieces[last].First(62).Trim());

                // From the top left round, the first corner again last; the ink as wide as the widest line and as high as the two.
                double left = vertices[0].Number(10);
                double top = vertices[0].Number(20);
                Assert.Equal(left, vertices[4].Number(10), 6);
                Assert.Equal(top, vertices[4].Number(20), 6);
                Assert.Equal(words.ActualWidth + 2.0 * gap, vertices[1].Number(10) - left, 6);
                Assert.Equal(left, vertices[3].Number(10), 6);
                Assert.Equal(top, vertices[1].Number(20), 6);
                Assert.Equal(vertices[2].Number(20), vertices[3].Number(20), 6);
                Assert.True(top - vertices[2].Number(20) > words.TextHeight + 2.0 * gap);
                Assert.Equal(30.0 - gap, left, 6);
            });
        }

        [Fact]
        public void TheWordsOfAMultileaderWithNoFrame_HaveNoPolylineRoundThem()
        {
            InFolder(folder =>
            {
                DxfDatabase database = Ezdxf();
                database.FontSearchPath = folder;
                DxfMLeader leader = Leader(database, false, DxfAttachmentPoint.TopLeft);

                List<Record> pieces = PiecesOf(Sections(R12(database)), leader.Handle);

                Assert.Equal(1, pieces.Count(r => r.Type == "POLYLINE"));
            });
        }

        [Fact]
        public void TheDoglegOfWordsHeldAtTheMiddleWithNoFrame_RunsOnToTheInkOfTheirFirstLine()
        {
            InFolder(folder =>
            {
                DxfDatabase database = Ezdxf();
                database.FontSearchPath = folder;
                DxfMLeader leader = Leader(database, false, DxfAttachmentPoint.TopCenter);
                DxfMText words = leader.Explode().OfType<DxfMText>().Single();

                List<Record> pieces = PiecesOf(Sections(R12(database)), leader.Handle);
                Record dogleg = pieces.First(r => r.Type == "LINE");
                Record first = pieces.First(r => r.Type == "TEXT");

                // The dogleg ends a dogleg's length past where the ink of the first line begins.
                double length = Math.Abs(dogleg.Number(11) - dogleg.Number(10));
                Assert.True(length > 2.0, "the dogleg runs on: " + length);
                Assert.Equal(first.Number(10), dogleg.Number(11) - 8.0, 6);
                Assert.True(words.ActualWidth > 0.0);

                // Held at the left it is the dogleg alone.
                DxfDatabase left = Ezdxf();
                left.FontSearchPath = folder;
                DxfMLeader plain = Leader(left, false, DxfAttachmentPoint.TopLeft);
                Record alone = PiecesOf(Sections(R12(left)), plain.Handle).First(r => r.Type == "LINE");
                Assert.Equal(8.0, Math.Abs(alone.Number(11) - alone.Number(10)), 6);
            });
        }

        // ---- an angular dimension -----------------------------------------------------------------------------------------------

        [Fact]
        public void TheWordsOfAnAngularDimension_AreOneTextOfAutoCadsOwnForm_AndTakeNoBlockNumber()
        {
            DxfDatabase database = Drawing();
            var picture = new DxfBlockTableRecord { Name = "*D" };
            database.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
            picture.AppendEntity(
                new DxfMText
                {
                    Location = new GeoPoint3(30, 30, 0),
                    TextHeight = 0.18,
                    Attachment = DxfAttachmentPoint.MiddleCenter,
                    Contents = "90\\U+00B0",
                }
            );
            var dimension = new DxfLineAngularDimension2(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(50, 0, 0),
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0),
                new GeoPoint3(30, 30, 0),
                string.Empty,
                null
            )
            {
                DimBlock = picture,
            };
            database.ModelSpace.AppendEntity(dimension);
            string name = picture.Name;

            string text = R12(database);
            Dictionary<string, List<Record>> sections = Sections(text);
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == name);
            Record[] drawn = blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToArray();

            // The line and the TEXT stand in the block, no INSERT and no block of pieces beside it.
            Assert.Equal(new[] { "LINE", "TEXT" }, drawn.Select(r => r.Type));
            Assert.DoesNotContain(blocks, r =>
                r.Type == "BLOCK" && r.First(2).StartsWith("*U", StringComparison.Ordinal)
            );

            // 8, 40, 5, 62 or not, 72 and 71 of nought, the place of six figures, the words with the degree sign written %%d: 2
            // heights before and 1.77 heights below where the MTEXT was held.
            Record words = drawn[1];
            Assert.Equal(new[] { 8, 40, 5 }, words.Groups.Take(3).Select(g => g.Key));
            Assert.Equal("90%%d", words.First(1));
            Assert.Equal("0", words.First(72));
            Assert.Equal("0", words.First(71));
            Assert.Equal("29.6400", words.First(10));
            Assert.Equal("29.6814", words.First(20));
            Assert.Equal("0.00000", words.First(30));
        }
    }
}
