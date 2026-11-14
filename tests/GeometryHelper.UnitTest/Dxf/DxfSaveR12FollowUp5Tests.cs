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
    /// What a drawing saved as R12 (AC1009) holds as AutoCAD 2021 writes it, found by probing its console (Database.DxfIn of the
    /// drawings in R2018 and Database.DxfOut at R12 of side databases, 259 cases): a stack under, over or through letters, in a
    /// TrueType font and beside a big font; a solid fill in a tilted plane; arcs, bulges and ellipses in a fill; an attribute of
    /// several lines; the pattern lines of two pattern lines and of a line a hole breaks. The fonts are written here, with the
    /// metrics of txt.shx and of Arial, so that the numbers asserted are AutoCAD's own.
    /// </summary>
    public class DxfSaveR12FollowUp5Tests
    {
        private const double Near = 1E-6;

        // ---- the text of a drawing as R12 ---------------------------------------------------------------------------------------

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

        private static string R12(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                return Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }
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
                }
                else if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    current = null;
                    record = null;
                }
                else if (current != null && code == 0)
                {
                    record = new Record(value);
                    current.Add(record);
                }
                else if (current != null)
                {
                    record?.Groups.Add(new KeyValuePair<int, string>(code, value));
                }
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

        private static void Is(double expected, double actual, double tolerance = Near) =>
            Assert.True(
                Math.Abs(expected - actual) <= tolerance,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

        private static void Text(Record piece, string words, double x, double y)
        {
            Assert.Equal("TEXT", piece.Type);
            Assert.Equal(words, piece.First(1));
            Is(x, piece.Number(10));
            Is(y, piece.Number(20));
        }

        private static void Line(Record piece, double x0, double y0, double x1, double y1, double tolerance = Near)
        {
            Assert.Equal("LINE", piece.Type);
            Is(x0, piece.Number(10), tolerance);
            Is(y0, piece.Number(20), tolerance);
            Is(x1, piece.Number(11), tolerance);
            Is(y1, piece.Number(21), tolerance);
        }

        // ---- the fonts ----------------------------------------------------------------------------------------------------------

        // A capital as txt.shx draws one: up 6, along 4, lifted, down 6 and along 2.
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        // A 1 as txt.shx draws it: up 6, along 2, lifted, down 6 and along 2.
        private static readonly byte[] One = { 1, 0x64, 0x20, 2, 0x6C, 0x20, 0 };

        // A letter of a big font 8 above as gbcbig.shx's.
        private static readonly byte[] Cell = { 2, 8, 1, 0, 1, 8, 6, 8, 2, 8, 1, 0xF8, 0 };

        private static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', Capital },
                { '1', One },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            for (char c = '2'; c <= '9'; c++)
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

        /// <summary>A Chinese big font as gbcbig.shx is made: a height above of nought and a depth of 4, bytes A1 to FE opening a letter of two; 中 (D6D0 in GBK) its letter.</summary>
        private static byte[] Chinese()
        {
            byte[] name = Encoding.ASCII.GetBytes("GH BIG").Concat(new byte[] { 0, 0, 4, 2, 0 }).ToArray();
            byte[] letter = new byte[] { 0 }
                .Concat(Cell)
                .ToArray();
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0\r\n\u001A"));
            bytes.AddRange(BitConverter.GetBytes((ushort)8));
            bytes.AddRange(BitConverter.GetBytes((ushort)3));
            bytes.AddRange(BitConverter.GetBytes((ushort)1));
            bytes.AddRange(BitConverter.GetBytes((ushort)0xA1));
            bytes.AddRange(BitConverter.GetBytes((ushort)0xFE));
            int place = bytes.Count + (3 * 8);
            bytes.AddRange(new byte[8]);
            bytes.AddRange(BitConverter.GetBytes((ushort)0));
            bytes.AddRange(BitConverter.GetBytes((ushort)name.Length));
            bytes.AddRange(BitConverter.GetBytes((uint)place));
            place += name.Length;
            bytes.AddRange(BitConverter.GetBytes((ushort)0xD6D0));
            bytes.AddRange(BitConverter.GetBytes((ushort)letter.Length));
            bytes.AddRange(BitConverter.GetBytes((uint)place));
            bytes.AddRange(name);
            bytes.AddRange(letter);
            return bytes.ToArray();
        }

        /// <summary>A TrueType font with the boxes of Arial's A, B, space and digits, in the units of Arial (A 1466 high), as AutoCAD 2021 measures them.</summary>
        private static byte[] Arial() =>
            TrueTypeFile.Font(
                new List<TrueTypeFile.Letter>
                {
                    new TrueTypeFile.Letter(0, 1366, 150, 0, 1257, 1466),
                    new TrueTypeFile.Letter(' ', 569),
                    new TrueTypeFile.Letter('A', 1366, -3, 0, 1369, 1466),
                    new TrueTypeFile.Letter('B', 1366, 150, 0, 1257, 1466),
                    new TrueTypeFile.Letter('V', 1366, 0, 0, 1366, 1466),
                    new TrueTypeFile.Letter('0', 1139, 85, -25, 1041, 1472),
                    new TrueTypeFile.Letter('1', 1139, 223, 0, 763, 1472),
                    new TrueTypeFile.Letter('2', 1139, 60, 0, 1031, 1472),
                }
            );

        private static void InFolder(Action<string> test, bool bigFont = false)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-r12f-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont());
                File.WriteAllBytes(Path.Combine(folder, "gh.ttf"), Arial());

                if (bigFont)
                {
                    File.WriteAllBytes(Path.Combine(folder, "gbcbig.shx"), Chinese());
                }

                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static DxfDatabase Drawing(string folder)
        {
            var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "TT", FileName = "gh.ttf" });
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "BIG",
                    FileName = "txt.shx",
                    BigFontFileName = "gbcbig.shx",
                }
            );
            return db;
        }

        /// <summary>An MTEXT in model space at (10, 10), held at its top left, its words, height and style given.</summary>
        private static DxfMText Words(DxfDatabase db, string contents, string style = null, double x = 10.0)
        {
            var mtext = new DxfMText
            {
                Contents = contents,
                TextHeight = 2.5,
                Attachment = DxfAttachmentPoint.TopLeft,
                Location = new GeoPoint3(x, 10, 0),
            };

            if (style != null)
            {
                mtext.TextStyleName = style;
            }

            db.ModelSpace.AppendEntity(mtext);
            return mtext;
        }

        // ---- a stack under, over or through letters -------------------------------------------------------------------------------

        [Fact]
        public void AStackUnderALine_IsLaidOut_TheNumeratorAndTheBarLiftedByAFifthOfTheHeight_EachHalfBeginningWithPercentPercentU()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "\\LA\\S1/2;B\\l");

                List<Record> pieces = PiecesOf(Sections(R12(db)), words.Handle);

                // AutoCAD 2021, txt.shx 2.5 high at the top left (10, 10): the line is 1 higher than the stack alone is, whose
                // denominator stands 0.4 of the height lower and whose numerator and bar stand 0.2 of it lower.
                Assert.Equal(new[] { "TEXT", "TEXT", "LINE", "TEXT", "TEXT" }, pieces.Select(p => p.Type));
                Text(pieces[0], "%%UA", 10.0, 3.1666666666666667);
                Text(pieces[1], "%%U1", 12.916666666666667, 7.0);
                Line(pieces[2], 12.5, 6.583333333333333, 14.166666666666667, 6.583333333333333);
                Text(pieces[3], "%%U2", 12.5, 3.1666666666666667);
                Text(pieces[4], "%%UB", 15.0, 3.1666666666666667);
            });
        }

        [Fact]
        public void AStackUnderAndOverAndOneWithNoDenominatorUnder_CountEachLineOnceAndEachHalfWithWords()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText both = Words(db, "\\L\\OA\\S1/2;B\\o\\l");
                DxfMText none = Words(db, "\\LA\\S12;B\\l", null, 100.0);

                Dictionary<string, List<Record>> sections = Sections(R12(db));
                List<Record> twice = PiecesOf(sections, both.Handle);
                List<Record> alone = PiecesOf(sections, none.Handle);

                // The halves of a stack both lined begin with the line over and then the line under, the letters with them the
                // other way round; each line is 0.2 of the height, so 0.4 where there are two.
                Text(twice[0], "%%U%%OA", 10.0, 2.1666666666666667);
                Text(twice[1], "%%O%%U1", 12.916666666666667, 6.5);

                // Of a stack with no denominator only the numerator's line lifts the top: A stands 0.2 of the height lower than
                // it would, 6.1667 and not 6.6667.
                Text(alone[0], "%%UA", 100.0, 6.1666666666666667);
            });
        }

        [Fact]
        public void LettersStruckThrough_AreOneTextWithTheLettersAboutThem_AndALineOfTheLinetypeContinuousAfterTheInsert()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "\\KAB\\k CD");

                Dictionary<string, List<Record>> sections = Sections(R12(db));
                List<Record> pieces = PiecesOf(sections, words.Handle);
                List<Record> entities = sections["ENTITIES"];

                // AutoCAD 2021: no %%K, one TEXT, and a LINE half the height above the baseline from where AB begins to where it
                // ends, written after the INSERT in the space.
                Assert.Single(pieces);
                Text(pieces[0], "AB CD", 10.0, 7.5);
                Assert.Equal(new[] { "INSERT", "LINE" }, entities.Select(r => r.Type));
                Line(entities[1], 10.0, 8.75, 15.0, 8.75);
                Assert.Equal("CONTINUOUS", entities[1].First(6));
            });
        }

        [Fact]
        public void TheLinesThroughStruckLettersOfSeveralTexts_StandAtTheEndOfTheSpace()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                Words(db, "\\KAB\\k CD");
                Words(db, "EF\\KGH\\k", null, 50.0);

                List<Record> entities = Sections(R12(db))["ENTITIES"];

                Assert.Equal(new[] { "INSERT", "INSERT", "LINE", "LINE" }, entities.Select(r => r.Type));
                Line(entities[2], 10.0, 8.75, 15.0, 8.75);
                Line(entities[3], 55.0, 8.75, 60.0, 8.75);
            });
        }

        [Fact]
        public void AStackStruckThrough_HasTheLinesAutoCadDrawsThroughItsHalves()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText begins = Words(db, "A\\K\\S1/2;\\kB");
                DxfMText follows = Words(db, "\\KA\\S1/2;B\\k", null, 100.0);

                Dictionary<string, List<Record>> sections = Sections(R12(db));
                List<Record> lines = sections["ENTITIES"].Where(r => r.Type == "LINE").ToList();

                // AutoCAD 2021: where the strike begins at the stack its numerator is lined through (12.9167 to 14.5833) and the
                // denominator has a line of no length; where struck letters come before it both halves have one, and so has
                // each struck letter after the stack, all at the end of the strike's first piece.
                Assert.Equal(6, lines.Count);
                Line(lines[0], 12.916666666666667, 8.75, 14.583333333333333, 8.75);
                Line(lines[1], 12.5, 5.416666666666667, 12.5, 5.416666666666667);
                Line(lines[2], 100.0, 5.416666666666667, 102.5, 5.416666666666667);
                Line(lines[3], 102.5, 7.5, 102.5, 7.5);
                Line(lines[4], 102.5, 5.416666666666667, 102.5, 5.416666666666667);
                Line(lines[5], 102.5, 5.416666666666667, 102.5, 5.416666666666667);
            });
        }

        // ---- a stack in a TrueType font and beside a big font -----------------------------------------------------------------

        [Fact]
        public void AStackInATrueTypeFont_StandsAsHighAsItsInk_AndBeginsWhereTheInkOfTheLettersBeforeIt_Ends()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "A\\S1/2;B", "TT");

                List<Record> pieces = PiecesOf(Sections(R12(db)), words.Handle);

                // AutoCAD 2021, Arial 2.5 high: its 1 and 2 reach 1472 units, not 1466, A is inked to 1369 though it moves the pen
                // 1366, and what follows the stack stands as far from it as the zero has room right of its ink (98 units).
                Assert.Equal(new[] { "TEXT", "TEXT", "LINE", "TEXT", "TEXT" }, pieces.Select(p => p.Type));
                Text(pieces[0], "A", 10.0, 4.146202819463392);
                Text(pieces[1], "1", 12.32179399727149, 7.489768076398362);
                Line(pieces[2], 12.334583901773531, 7.073101409731695, 13.99045020463847, 7.073101409731695);
                Text(pieces[3], "2", 12.232264665757160, 4.146202819463392);
                Text(pieces[4], "B", 14.157571623465209, 4.146202819463392);
            });
        }

        [Fact]
        public void ASlashInATrueTypeFont_FollowsTheInkOfItsNumeratorByAThirdOfTheHeight()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "A\\S1#2;B", "TT");

                List<Record> pieces = PiecesOf(Sections(R12(db)), words.Handle);

                Assert.Equal(new[] { "TEXT", "TEXT", "LINE", "TEXT", "TEXT" }, pieces.Select(p => p.Type));
                Text(pieces[0], "A", 10.0, 4.979536152796725);
                Text(pieces[1], "1", 11.954297407912691, 7.489768076398362);
                Line(pieces[2], 12.238182601709919, 5.441887965675883, 15.106064783465151, 9.537648187120843);
                Text(pieces[3], "2", 13.986471123237839, 4.979536152796725);
                Text(pieces[4], "B", 15.911778080945879, 4.979536152796725);
            });
        }

        [Fact]
        public void AStackBesideABigFont_IsLaidOutAsBesideNone()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    DxfMText words = Words(db, "A\\S1/2;B", "BIG");

                    List<Record> pieces = PiecesOf(Sections(R12(db)), words.Handle);

                    Assert.Equal(new[] { "TEXT", "TEXT", "LINE", "TEXT", "TEXT" }, pieces.Select(p => p.Type));
                    Text(pieces[0], "A", 10.0, 4.166666666666667);
                    Text(pieces[1], "1", 12.916666666666667, 7.5);
                    Line(pieces[2], 12.5, 7.083333333333333, 14.166666666666667, 7.083333333333333);
                    Text(pieces[3], "2", 12.5, 4.166666666666667);
                    Text(pieces[4], "B", 15.0, 4.166666666666667);
                },
                true
            );
        }

        // ---- a solid fill in a tilted plane -----------------------------------------------------------------------------------

        private static GeoPoint2[] Rectangle(double x0, double y0, double x1, double y1) =>
            new[] { new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1) };

        private static DxfHatch Fill(DxfDatabase database, params DxfHatchLoop[] loops)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");

            foreach (DxfHatchLoop loop in loops)
            {
                hatch.AppendLoop(loop);
            }

            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        private static DxfHatchLoop Loop(params object[] curves) =>
            new DxfHatchLoop(DxfHatchLoopTypes.External, curves);

        private static GeoVector3 Up => new GeoVector3(0, 0, 1);

        private static GeoArc3 Arc(double radius, double from, double to) =>
            // An arc's angle is counted from the y axis here: AutoCAD's from the x axis is a quarter turn on.
            new GeoArc3(new GeoPoint3(0, 0, 0), Up, radius, from - (Math.PI / 2.0), to - (Math.PI / 2.0));

        private static GeoLine3 Segment(double x0, double y0, double x1, double y1) =>
            new GeoLine3(x0, y0, 0, x1, y1, 0);

        private static List<Record> Fills(DxfDatabase database, DxfHatch hatch) =>
            PiecesOf(Sections(R12(database)), hatch.Handle);

        [Fact]
        public void ASolidFillInATiltedPlane_IsTheSolidsAndLinesOfItsStrips_InTheWorld_TheSolidsInThePlaneOfTheOppositeNormal()
        {
            var database = new DxfDatabase("AC1032");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.Normal = new GeoVector3(1, 1, 1);
            hatch.AppendLoop(DxfHatchLoopTypes.External, Rectangle(0, 0, 100, 60), new double[4]);
            database.ModelSpace.AppendEntity(hatch);

            List<Record> pieces = Fills(database, hatch);

            // AutoCAD 2021: the strips of the rectangle in its plane, two SOLIDs whose extrusion is the normal turned the other
            // way, their corners as that plane reads the world points, and the LINEs of its sides, from (0, 0, 0) along the plane's
            // y axis, in the world.
            Assert.Equal(new[] { "SOLID", "SOLID", "LINE", "LINE" }, pieces.Select(p => p.Type));
            Assert.All(pieces.Take(2), s => Is(-0.5773502691896258, s.Number(210)));
            Assert.All(pieces.Take(2), s => Is(-0.5773502691896258, s.Number(230)));
            Is(0.0, pieces[0].Number(10));
            Is(60.0, pieces[0].Number(20));
            Is(-100.0, pieces[0].Number(11));
            Is(0.0, pieces[0].Number(21));
            Line(pieces[2], 0.0, 0.0, -24.49489742783179, -24.49489742783179);
            Is(48.98979485566357, pieces[2].Number(31));
        }

        // ---- arcs, bulges and ellipses in a fill ------------------------------------------------------------------------------

        [Theory]
        [InlineData(0.5, 18, 18)]
        public void ABulgeInAFill_IsCutIntoTheChordsAutoCadCutsItInto(double bulge, int solids, int lines)
        {
            var database = new DxfDatabase("AC1032");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(DxfHatchLoopTypes.External, Rectangle(0, 0, 100, 60), new[] { bulge, 0.0, 0.0, 0.0 });
            database.ModelSpace.AppendEntity(hatch);

            // AutoCAD 2021: the fewest chords that stand 0.1 off the arc at the most, 17 for a bulge of 0.5 over a chord of 100
            // (an arc of 106.26 degrees, radius 62.5): 18 SOLIDs and 18 LINEs for the rectangle with it.
            List<Record> pieces = Fills(database, hatch);
            Assert.Equal(solids, pieces.Count(p => p.Type == "SOLID"));
            Assert.Equal(lines, pieces.Count(p => p.Type == "LINE"));
        }

        [Theory]
        [InlineData(10.0, 0.9, true, 9, 10)]
        [InlineData(200.0, 0.5, true, 15, 16)]
        [InlineData(5.0, 1.0, false, 10, 12)]
        public void AnArcInAFill_IsCutIntoTheChordsAutoCadCutsItInto(
            double radius,
            double sweep,
            bool pie,
            int solids,
            int lines
        )
        {
            var database = new DxfDatabase("AC1032");
            DxfHatch hatch = Fill(
                database,
                pie
                    ? Loop(
                        Segment(0, 0, radius, 0),
                        Arc(radius, 0, sweep),
                        Segment(radius * Math.Cos(sweep), radius * Math.Sin(sweep), 0, 0)
                    )
                    : Loop(
                        Arc(radius, 0, sweep),
                        Segment(radius * Math.Cos(sweep), radius * Math.Sin(sweep), radius, 0)
                    )
            );

            // A pie or a segment of a circle: at least the share of 33 chords the sweep is of a turn, at most 0.1 off the circle
            // (6 chords for 1 radian of a circle of radius 5).
            List<Record> pieces = Fills(database, hatch);
            Assert.Equal(solids, pieces.Count(p => p.Type == "SOLID"));
            Assert.Equal(lines, pieces.Count(p => p.Type == "LINE"));
        }

        [Theory]
        [InlineData(1.0, 0.0, 50.0, 25.0, 48, 48)]
        [InlineData(1.0, 1.0, 50.0, 20.0, 96, 98)]
        public void AnEllipseInAFill_IsCutIntoAsManyChordsAsACircleOfItsMajorRadius(
            double ax,
            double ay,
            double major,
            double minor,
            int solids,
            int lines
        )
        {
            var database = new DxfDatabase("AC1032");
            DxfHatch hatch = Fill(
                database,
                Loop(new GeoEllipse3(new GeoPoint3(0, 0, 0), Up, new GeoVector3(ax, ay, 0), major, minor))
            );

            // 50 chords for a major radius of 50, a parameter step of 7.2 degrees each, whatever the minor radius.
            List<Record> pieces = Fills(database, hatch);
            Assert.Equal(solids, pieces.Count(p => p.Type == "SOLID"));
            Assert.Equal(lines, pieces.Count(p => p.Type == "LINE"));
        }

        // ---- an attribute of several lines ------------------------------------------------------------------------------------

        [Fact]
        public void AnAttributeOfTwoLines_IsOneAttributeForEachLine_TagsEndingInNumbers_TheSecondOnesPlaceLockedInTheInsert()
        {
            var database = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "BOLT" };
            database.BlockTable.Add(block);
            var definition = new DxfAttributeDefinition(new GeoPoint3(0, -5, 0), "M20", "MARK", "Mark", null);
            block.AppendEntity(definition);
            definition.MTextAttributeDefinition = new DxfMText
            {
                Contents = "M20\\Pgrade 8.8",
                Location = new GeoPoint3(0, -5, 0),
                TextHeight = 2.5,
            };
            var insert = new DxfBlockReference(new GeoPoint3(100, 100, 0), block);
            database.ModelSpace.AppendEntity(insert);
            var attribute = new DxfAttributeReference();
            attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
            attribute.MTextAttribute = new DxfMText
            {
                Contents = "M20\\Pgrade 8.8",
                Location = new GeoPoint3(100, 95, 0),
                TextHeight = 2.5,
            };
            insert.AttributeCollection.AppendAttribute(attribute);

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            List<Record> definitions = sections["BLOCKS"].Where(r => r.Type == "ATTDEF").ToList();
            List<Record> attributes = sections["ENTITIES"].Where(r => r.Type == "ATTRIB").ToList();

            // AutoCAD 2021: M20 at the baseline 2.5 under the top, then grade 8.8 a line and two thirds of a height lower; the
            // second line has the extended data AcDbAttr of version 0 and a position locked in the INSERT only.
            Assert.Equal(new[] { "MARK_001", "MARK_002" }, definitions.Select(r => r.First(2)));
            Assert.Equal(new[] { "M20", "grade 8.8" }, definitions.Select(r => r.First(1)));
            Is(-7.5, definitions[0].Number(20));
            Is(-11.666666666666666, definitions[1].Number(20));
            Assert.False(definitions[0].Has(7));
            Assert.False(definitions[0].Has(11));
            Assert.False(definitions[1].Has(1001));
            Assert.Equal(new[] { "MARK_001", "MARK_002" }, attributes.Select(r => r.First(2)));
            Is(92.5, attributes[0].Number(20));
            Is(88.33333333333333, attributes[1].Number(20));
            Assert.False(attributes[0].Has(1001));
            Assert.Equal("AcDbAttr", attributes[1].First(1001));
        }

        // ---- the lines of a pattern ---------------------------------------------------------------------------------------------

        private static DxfPatternDefinition Pattern(double degrees)
        {
            double angle = degrees * Math.PI / 180.0;
            return new DxfPatternDefinition(angle, 0, 0, -0.125 * Math.Sin(angle), 0.125 * Math.Cos(angle), null);
        }

        private static List<Record> PatternLines(params GeoPoint2[][] loops)
        {
            var database = new DxfDatabase("AC1032");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI37");
            hatch.SetPatternDefinitions(new[] { Pattern(45), Pattern(135) });

            for (int i = 0; i < loops.Length; i++)
            {
                hatch.AppendLoop(
                    i == 0 ? DxfHatchLoopTypes.External : DxfHatchLoopTypes.Default,
                    loops[i],
                    new double[loops[i].Length]
                );
            }

            hatch.PatternScale = loops.Length == 1 ? 96.0 : 48.0;
            database.ModelSpace.AppendEntity(hatch);
            return Fills(database, hatch);
        }

        [Fact]
        public void ThePatternLinesOfAHatch_AreWrittenTheLastPatternLineFirst()
        {
            List<Record> lines = PatternLines(Rectangle(0, 0, 100, 60));

            // AutoCAD 2021, ANSI37 (45 and 135 degrees) at 96: the lines at 135 degrees from the top right corner down, then
            // those at 45 degrees from the bottom right corner up.
            Assert.Equal(18, lines.Count);
            Line(lines[0], 100.0, 52.74, 92.74, 60.0, 6E-3);
            Line(lines[3], 100.0, 1.82, 41.82, 60.0, 6E-3);
            Line(lines[9], 84.85, 0.0, 100.0, 15.15, 6E-3);
            Line(lines[17], 0.0, 50.91, 9.09, 60.0, 6E-3);
        }

        [Fact]
        public void TheTwoPiecesOfAPatternLineThatAHoleBreaks_AreWrittenTheSecondFirst()
        {
            List<Record> lines = PatternLines(Rectangle(0, 0, 100, 60), Rectangle(30, 20, 70, 40));

            // The line at 135 degrees that crosses the hole: its piece beyond the hole before the one before it.
            Assert.Equal(51, lines.Count);
            Line(lines[6], 61.82, 40.0, 41.82, 60.0, 6E-3);
            Line(lines[7], 100.0, 1.82, 70.0, 31.82, 6E-3);
        }

        // ---- the colours of an MTEXT's runs ----------------------------------------------------------------------------------

        [Fact]
        public void AColourCodeInAnMText_GivesTheTextOfItsRunTheColourIndexAsTheGroup62()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "\\C1;AB\\C2;CD");

                List<Record> pieces = PiecesOf(Sections(R12(db)), words.Handle);

                // AutoCAD 2021: a TEXT for each run, each with the colour of its code as its group 62.
                Assert.Equal(new[] { "AB", "CD" }, pieces.Select(p => p.First(1)));
                Assert.Equal(new[] { "1", "2" }, pieces.Select(p => p.First(62).Trim()));
            });
        }

        [Fact]
        public void ATrueColourCode_GivesTheIndexNearestItsBlueGreenRed_AndTheSameColourTwiceIsOneRun()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText blue = Words(db, "\\c16711680;AB CD");
                DxfMText twice = Words(db, "\\C3;AB\\C3;CD", null, 100.0);
                DxfMText back = Words(db, "{\\C1;AB}CD", null, 200.0);

                Dictionary<string, List<Record>> sections = Sections(R12(db));

                // 16711680 is 0xFF0000 read with the red lowest: blue, the index 5; \C3 twice is one TEXT of 3; after the braces the
                // letters keep the MTEXT's own colour, which R12 leaves out.
                Assert.Equal("5", PiecesOf(sections, blue.Handle).Single().First(62).Trim());
                Record green = PiecesOf(sections, twice.Handle).Single();
                Assert.Equal("ABCD", green.First(1));
                Assert.Equal("3", green.First(62).Trim());
                List<Record> braced = PiecesOf(sections, back.Handle);
                Assert.Equal("1", braced[0].First(62).Trim());
                Assert.False(braced[1].Has(62));
            });
        }

        [Fact]
        public void ALineThroughStruckLetters_HasTheColourOfTheirRun()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfMText words = Words(db, "\\C3;\\KAB\\k CD");

                Dictionary<string, List<Record>> sections = Sections(R12(db));

                Assert.Equal("3", PiecesOf(sections, words.Handle).Single().First(62).Trim());
                Assert.Equal("3", sections["ENTITIES"].Single(r => r.Type == "LINE").First(62).Trim());
            });
        }
    }
}
