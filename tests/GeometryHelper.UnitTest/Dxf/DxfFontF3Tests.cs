using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The big fonts of a drawing's text styles (<see cref="DxfTextStyleTableRecord.BigFontFileName"/>, check F3): big fonts
    /// written by the tests in AutoCAD's form, no Autodesk font in the repository, their letters drawn as gbcbig.shx and
    /// extfont.shx draw theirs (中 a cell of 8 units of a height above of nought, read as 8, and of 18 units of 15 in an
    /// extended big font, its mouth a shape stretched into a box), beside a unifont txt.shx drawn as txt.shx draws its
    /// letters, so that the numbers AutoCAD 2021 gave for those fonts are asserted.
    /// </summary>
    public class DxfFontF3Tests
    {
        private const double Near = 1E-9;

        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        // ---- the fonts ---------------------------------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down where it starts, up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>A letter of a big font 8 above as gbcbig.shx's: from (1, 0) to (7, 8), the pen moved on to 8.</summary>
        private static readonly byte[] Cell = { 2, 8, 1, 0, 1, 8, 6, 8, 2, 8, 1, 0xF8, 0 };

        /// <summary>The letters of the unifont txt.shx, 6 above and 2 below, by their Unicode numbers.</summary>
        private static Dictionary<int, byte[]> Letters()
        {
            var letters = new Dictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', new byte[] { 1, 0x64, 0x30, 2, 0x6C, 0x20, 0 } },
                { '!', new byte[] { 1, 0x64, 2, 0x6C, 0x20, 0 } },
                { '.', new byte[] { 1, 2, 0x20, 0 } },
                { 'I', new byte[] { 2, 0x10, 1, 0x64, 2, 0x6C, 0x20, 0 } },
                { '~', Capital },
                { 0xB0, new byte[] { 2, 0x44, 1, 0x22, 2, 0x6C, 0x10, 0 } },
                { 0x0414, Capital },
                { 0x0416, Capital },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (c != 'I')
                {
                    letters.Add(c, Capital);
                }
            }

            return letters;
        }

        /// <summary>The bytes of a unifont of AutoCAD's form: its header (name, above, below, modes, encoding, embedding), then each letter, its number, length, name and codes.</summary>
        private static byte[] Unifont(IDictionary<int, byte[]> letters)
        {
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

        /// <summary>
        /// The bytes of a big font of AutoCAD's form: its header, the length of an entry (8), the count of entries and of ranges,
        /// the ranges of the bytes that open a letter of two, an empty entry and then each letter's number, length and place, then
        /// the letters, shape 0 its name, above, below and modes, or in an extended big font its cell's height, nought, modes, its
        /// width and nought.
        /// </summary>
        private static byte[] BigFont(
            int above,
            int below,
            int[] ranges,
            IDictionary<int, byte[]> letters,
            int cellWidth = 0
        )
        {
            byte[] tail =
                cellWidth > 0
                    ? new byte[] { (byte)above, 0, 2, (byte)cellWidth, 0 }
                    : new byte[] { (byte)above, (byte)below, 2, 0 };
            var records = new List<KeyValuePair<int, byte[]>>
            {
                new KeyValuePair<int, byte[]>(
                    0,
                    Encoding.ASCII.GetBytes("GH BIG").Concat(new byte[] { 0 }).Concat(tail).ToArray()
                ),
            };
            records.AddRange(
                letters
                    .OrderBy(l => l.Key)
                    .Select(l => new KeyValuePair<int, byte[]>(
                        l.Key,
                        new byte[] { 0 }
                            .Concat(l.Value)
                            .ToArray()
                    ))
            );
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0\r\n\u001A"));
            int count = records.Count + 1;
            bytes.AddRange(BitConverter.GetBytes((ushort)8));
            bytes.AddRange(BitConverter.GetBytes((ushort)count));
            bytes.AddRange(BitConverter.GetBytes((ushort)(ranges.Length / 2)));

            foreach (int value in ranges)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)value));
            }

            int place = bytes.Count + (count * 8);
            bytes.AddRange(new byte[8]);

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Value.Length));
                bytes.AddRange(BitConverter.GetBytes((uint)place));
                place += record.Value.Length;
            }

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                bytes.AddRange(record.Value);
            }

            return bytes.ToArray();
        }

        /// <summary>A Chinese big font as gbcbig.shx is made: a height above of nought and a depth of 4, bytes A1 to FE opening a letter of two; 中 (D6D0 in GBK), Ж (A7A8) and ° (A1E3) its letters.</summary>
        private static byte[] Chinese() =>
            BigFont(
                0,
                4,
                new[] { 0xA1, 0xFE },
                new Dictionary<int, byte[]>
                {
                    { 0xD6D0, Cell },
                    { 0xA7A8, Cell },
                    { 0xA1E3, Cell },
                }
            );

        /// <summary>
        /// A Japanese extended big font as extfont.shx is made: a cell 14 wide and 15 high, 81 to 9F opening a letter of two; a
        /// space 9 wide, A, a half-width ｱ (B1) and a question mark of its own; 中 (9286) its mouth a box of the cell stretched
        /// from (1, 6) into 12 by 6 and its stroke down from (7, 15); 。 (8142) a dot and a whole circle; and the box shape FD01.
        /// </summary>
        private static byte[] Japanese() =>
            BigFont(
                15,
                0,
                new[] { 0x81, 0x9F },
                new Dictionary<int, byte[]>
                {
                    { 0x20, new byte[] { 2, 0x90, 0 } },
                    { 0x3F, new byte[] { 2, 8, 2, 0, 1, 8, 5, 10, 2, 8, 3, 0xF6, 0 } },
                    { 0x41, new byte[] { 2, 8, 1, 0, 1, 8, 20, 15, 2, 8, 1, 0xF1, 0 } },
                    { 0xB1, new byte[] { 2, 8, 1, 0, 1, 8, 8, 10, 2, 8, 1, 0xF6, 0 } },
                    { 0x9286, new byte[] { 2, 7, 0, 0xFD, 0x01, 1, 6, 12, 6, 2, 8, 7, 15, 1, 0xFC, 2, 8, 11, 0, 0 } },
                    { 0x8142, new byte[] { 2, 8, 3, 3, 1, 10, 2, 0x00, 2, 8, 15, 0xFD, 0 } },
                    { 0xFD01, new byte[] { 2, 8, 0, 0, 1, 0xF4, 0xE0, 0xFC, 0xE8, 2, 8, 18, 0, 0 } },
                },
                14
            );

        /// <summary>A big font of symbols as special.shx is made, 6 above: "~" opening a letter of two, "~!" (7E21) a line 4 long 3 up.</summary>
        private static byte[] Symbols() =>
            BigFont(
                6,
                2,
                new[] { 0x7E, 0x7E },
                new Dictionary<int, byte[]> { { 0x7E21, new byte[] { 2, 8, 0, 3, 1, 0x40, 2, 8, 2, 0xFD, 0 } } }
            );

        /// <summary>A folder of its own holding txt.shx and the files given; deleted after the test.</summary>
        private static void InFolder(Action<string> test, params KeyValuePair<string, byte[]>[] files)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-bigfonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont(Letters()));

                foreach (KeyValuePair<string, byte[]> file in files)
                {
                    File.WriteAllBytes(Path.Combine(folder, file.Key), file.Value);
                }

                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static KeyValuePair<string, byte[]> F(string name, byte[] bytes) =>
            new KeyValuePair<string, byte[]>(name, bytes);

        /// <summary>A new drawing of R2018, its fonts looked for in the folder given (none where null), its style BIG of txt.shx and the big font given.</summary>
        private static DxfDatabase Drawing(string folder, string bigFont, string font = "txt.shx")
        {
            var db = new DxfDatabase("AC1032");
            db.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "BIG",
                    FileName = font,
                    BigFontFileName = bigFont,
                }
            );

            if (folder != null)
            {
                db.FontSearchPath = folder;
            }

            return db;
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Is(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= Near,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

        /// <summary>A TEXT 4 high in model space in the style BIG, its modes and second point given.</summary>
        private static DxfDBText Text(
            DxfDatabase db,
            string words,
            int horizontal,
            int vertical,
            GeoPoint3 second,
            double height = 4.0
        )
        {
            var text = new DxfDBText
            {
                TextString = words,
                Height = height,
                TextStyleName = "BIG",
            };
            text.HorizontalMode = (DxfTextHorizontalMode)horizontal;
            text.VerticalMode = (DxfTextVerticalMode)vertical;
            text.AlignmentPoint = second;
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        /// <summary>A TEXT 4 high at the origin, left on its baseline, in the style BIG.</summary>
        private static GeoAabb3 Ink(DxfDatabase db, string words)
        {
            var text = new DxfDBText
            {
                TextString = words,
                Height = 4,
                TextStyleName = "BIG",
                Position = P(0, 0),
            };
            db.ModelSpace.AppendEntity(text);
            return text.GeometricExtents;
        }

        /// <summary>The messages logged while an action runs, every level, each after its level.</summary>
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

        // ---- the tests ---------------------------------------------------------------------------------------------------------

        [Fact]
        public void ALetterOfTwoBytes_IsDrawnFromTheBigFont_ByItsCodeInTheCodePageOfTheBigFontsName()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): beside gbcbig.shx, 中 is its letter D6D0, of GBK, at 4 over 8; "A中B" centred on
                    // (100, 50) is inked from 0 to 10.667, A's 4, 中's 4 and B's ink.
                    DxfDatabase db = Drawing(folder, "gbcbig.shx");
                    Is(P(94.666666666666671, 50), Text(db, "A中B", 1, 0, P(100, 50)).Position);
                    Is(P(98, 50), Text(db, "中", 1, 0, P(100, 50)).Position);
                    GeoAabb3 box = Ink(db, "中");
                    Is(P(0.5, 0), box.Min);
                    Is(P(3.5, 4), box.Max);

                    // The name gives the code page, of any case and without its extension: the same file as chineset.shx reads 中
                    // in Big5 (A4A4), which it has not, and draws txt's question mark, 2 wide.
                    Is(P(98, 50), Text(Drawing(folder, "GBCBIG"), "中", 1, 0, P(100, 50)).Position);
                    Is(P(99, 50), Text(Drawing(folder, "chineset.shx"), "中", 1, 0, P(100, 50)).Position);

                    // A big font of another name, or named with its folder, AutoCAD reads in no code page (F3: a copy of
                    // gbcbig.shx as foo.shx draws 中 as its question mark): its letters beyond ASCII are not measured.
                    GeoPoint3 unmeasured = Text(Drawing(null, "gbcbig.shx"), "中", 1, 0, P(100, 50)).Position;
                    Is(unmeasured, Text(Drawing(folder, "mybig.shx"), "中", 1, 0, P(100, 50)).Position);
                    Is(
                        unmeasured,
                        Text(Drawing(folder, Path.Combine(folder, "gbcbig.shx")), "中", 1, 0, P(100, 50)).Position
                    );
                    Is(P(94.666666666666671, 50), Text(Drawing(folder, "mybig.shx"), "ABC", 1, 0, P(100, 50)).Position);
                },
                F("gbcbig.shx", Chinese()),
                F("chineset.shx", Chinese()),
                F("mybig.shx", Chinese())
            );
        }

        [Fact]
        public void ABigFontOfAHeightOfNought_IsEightHigh_AndATextAtItsBottom_StandsOnTheBigFontsDepth()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F1a, F3): gbcbig.shx, of a height above of nought and a depth of 64, holds a text 4 high at its
                    // bottom 32 up, whatever its letters. This one, of a depth of 4, 2 up; the extended Japanese one, of none, on
                    // its baseline, and txt's own 1.333 up where its big font is in no folder.
                    Is(P(100, 52), Text(Drawing(folder, "gbcbig.shx"), "A", 0, 1, P(100, 50)).Position);
                    Is(P(96.666666666666671, 52), Text(Drawing(folder, "gbcbig.shx"), "AB", 1, 1, P(100, 50)).Position);
                    Is(P(100, 50), Text(Drawing(folder, "extfont.shx"), "A", 0, 1, P(100, 50)).Position);
                },
                F("gbcbig.shx", Chinese()),
                F("extfont.shx", Japanese())
            );
        }

        [Fact]
        public void ASpace_IsTheBigFontsWhereItHasOne_ALetterOfOneByte_TheFontsFirst_AndAHalfWidthKana_TheBigFonts()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): extfont.shx's space is 9 of 15 wide, so that "A B" 4 high is inked to 9.067, not 10.667;
                    // its A is passed over for txt's.
                    DxfDatabase db = Drawing(folder, "extfont.shx");
                    Is(9.0666666666666664, Ink(db, "A A").Max.X);
                    Is(2.6666666666666665, Ink(db, "A").Max.X);

                    // ｱ is B1 of Shift-JIS alone, which txt has not: the big font's, inked from 1 to 9 of 15.
                    GeoAabb3 kana = Ink(db, "ｱ");
                    Is(P(0.26666666666666666, 0), kana.Min);
                    Is(P(2.4, 2.6666666666666665), kana.Max);
                },
                F("extfont.shx", Japanese())
            );
        }

        [Fact]
        public void ALetterOfTwoBytesTheBigFontHas_IsItsBeforeTheFonts_ButTheSignsOfDegreeAndPlusMinus()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): Ж beside extfont.shx is its 8447, though txt.shx has one; beside gbcbig.shx, which has
                    // not A7A8, txt's. The degree sign is txt's though gbcbig.shx has A1E3.
                    DxfDatabase db = Drawing(folder, "gbcbig.shx");
                    Is(P(0.5, 0), Ink(db, "Ж").Min);
                    Is(P(0, 0), Ink(db, "Д").Min);
                    Is(P(2.6666666666666665, 4), Ink(db, "Д").Max);
                    GeoAabb3 degree = Ink(db, "°");
                    Is(P(0, 2.6666666666666665), degree.Min);
                    Is(P(1.3333333333333333, 4), degree.Max);
                    Is(P(1.3333333333333333, 4), Ink(db, "%%d").Max);
                },
                F("gbcbig.shx", Chinese())
            );
        }

        [Fact]
        public void ALetterNeitherFontHas_IsTheBigFontsQuestionMarkWhereItHasOne_ElseTheFonts()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): あ beside gbcbig.shx, which has not A4A2, is txt's question mark, 2 wide; 한, which
                    // Shift-JIS has not, beside a big font with a question mark is that one.
                    Is(P(2, 4), Ink(Drawing(folder, "gbcbig.shx"), "あ").Max);
                    GeoAabb3 mark = Ink(Drawing(folder, "extfont.shx"), "한");
                    Is(P(0.53333333333333333, 0), mark.Min);
                    Is(P(1.8666666666666667, 2.6666666666666665), mark.Max);
                },
                F("gbcbig.shx", Chinese()),
                F("extfont.shx", Japanese())
            );
        }

        [Fact]
        public void AByteOpeningALetterOfTwo_TakesTheNextLetter_AndAWordEndingInASpaceSoTaken_EndsInASpace()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): special.shx's "~!" is its letter 7E21; "~ ", of no letter 7E20, is one question mark,
                    // yet it ends in a space: centred on (100, 50) from 0 to its advance of 3.333, it stands at 98.333.
                    DxfDatabase db = Drawing(folder, "symbols.shx");
                    GeoAabb3 tilde = Ink(db, "~!");
                    Is(P(0, 2), tilde.Min);
                    Is(P(2.6666666666666665, 2), tilde.Max);
                    Is(6.6666666666666661, Ink(db, "A~!").Max.X);
                    Is(P(98.333333333333329, 50), Text(db, "~ ", 1, 0, P(100, 50)).Position);
                    Is(P(98.666666666666671, 50), Text(db, "~", 1, 0, P(100, 50)).Position);
                },
                F("symbols.shx", Symbols())
            );
        }

        [Fact]
        public void AShapeOfAnExtendedBigFont_IsStretchedIntoItsBox_AndABigFontsWholeCircle_IsNotDrawn()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): extfont.shx's 中 4 high is inked from 0.267 to 3.467 and moves the pen 4.8, its mouth
                    // stretched from (1, 6) into 12 by 6 and its stroke from the pen put back.
                    DxfDatabase db = Drawing(folder, "extfont.shx");
                    GeoAabb3 middle = Ink(db, "中");
                    Is(P(0.26666666666666666, 0), middle.Min);
                    Is(P(3.4666666666666668, 4), middle.Max);
                    Is(P(95.733333333333334, 50), Text(db, "中中", 1, 0, P(100, 50)).Position);

                    // chineset.shx's 。 is its dot alone: the whole circle about it is not drawn. This one's (8142) at (3, 3).
                    GeoAabb3 stop = Ink(db, "。");
                    Is(P(0.8, 0.8), stop.Min);
                    Is(P(0.8, 0.8), stop.Max);
                },
                F("extfont.shx", Japanese())
            );
        }

        [Fact]
        public void ALetterOfTheFontAfterTheBigFonts_ThatPutsItsPenDownWhereItStarts_IsInkedWhereTheyBegan()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3, F1a's "not understood"): txt and gbcbig "中A" 4 high are boxed from 0, not from 中's ink
                    // at 0.529; " 中A" from 4, where 中 began; "中I", I moving its pen before it puts it down, from 中's ink.
                    DxfDatabase db = Drawing(folder, "gbcbig.shx");
                    Is(0, Ink(db, "中A").Min.X);
                    Is(0, Ink(db, "中.").Min.X);
                    Is(4, Ink(db, " 中A").Min.X);
                    Is(0.5, Ink(db, "中I").Min.X);
                    Is(P(96.666666666666671, 50), Text(db, "中A", 1, 0, P(100, 50)).Position);

                    // An MTEXT is not boxed so: "中A" is 6.167 wide, from 中's ink.
                    var mtext = new DxfMText
                    {
                        Contents = "中A",
                        TextHeight = 4,
                        TextStyleName = "BIG",
                        Location = P(0, 0),
                    };
                    db.ModelSpace.AppendEntity(mtext);
                    Is(6.1666666666666661, mtext.ActualWidth);
                },
                F("gbcbig.shx", Chinese())
            );
        }

        [Fact]
        public void AnMTextBesideABigFont_IsMeasured_ButNotALetterNeitherFontHasNorAnIdeographicSpace()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder, "gbcbig.shx");
                    DxfMText Made(string contents)
                    {
                        var mtext = new DxfMText
                        {
                            Contents = contents,
                            TextHeight = 4,
                            TextStyleName = "BIG",
                            Location = P(0, 0),
                        };
                        db.ModelSpace.AppendEntity(mtext);
                        return mtext;
                    }

                    // AutoCAD 2021 (F3): gbcbig.shx's "A中B" is as wide as its ink, and as high as its letters.
                    DxfMText words = Made("A中B");
                    Is(10.666666666666666, words.ActualWidth);
                    Is(4, words.ActualHeight);

                    // あ, which neither font has, AutoCAD draws from another font in an MTEXT; an ideographic space it spaces
                    // otherwise; and a multibyte code it draws from another font: none is measured.
                    Assert.Equal(0.0, Made("あ").ActualWidth);
                    Assert.Equal(0.0, Made((char)0x3000 + "中").ActualWidth);
                    Assert.Equal(0.0, Made("\\M+5D6D0").ActualWidth);
                },
                F("gbcbig.shx", Chinese())
            );
        }

        [Fact]
        public void ATrueTypeStylesBigFont_IsNotDrawn_AndItsTextAtItsBottom_StandsOnTheTrueTypeFont()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): arial.ttf beside gbcbig.shx draws as arial.ttf alone; a text at its bottom stands 7 times
                    // the usWinDescent of 210 over A's 700 up, as F2 found.
                    DxfDatabase db = Drawing(folder, "gbcbig.shx", "gh.ttf");
                    Is(P(100, 52.1), Text(db, "A", 0, 1, P(100, 50), 7).Position);
                },
                F("gh.ttf", TrueTypeFile.Font(TrueTypeFile.Letters())),
                F("gbcbig.shx", Chinese())
            );
        }

        [Fact]
        public void AFontOfShapesBesideABigFont_IsNotMeasured()
        {
            InFolder(
                folder =>
                {
                    // AutoCAD 2021 (F3): times.shx beside gbcbig.shx inked "~!" a third of a unit higher than its glyphs read.
                    GeoPoint3 unmeasured = Text(
                        Drawing(null, "gbcbig.shx", "shapes.shx"),
                        "AB",
                        1,
                        0,
                        P(100, 50)
                    ).Position;
                    Is(unmeasured, Text(Drawing(folder, "gbcbig.shx", "shapes.shx"), "AB", 1, 0, P(100, 50)).Position);
                },
                F("shapes.shx", Shapes()),
                F("gbcbig.shx", Chinese())
            );
        }

        [Fact]
        public void ABigFontInNoFolder_IsSaidOnceAtDebug_AndItsTextsAreMeasuredWhereEveryLetterIsTheFonts()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder, "gbcbig.shx");
                GeoPoint3 unmeasured = Text(Drawing(null, "gbcbig.shx"), "中", 1, 0, P(100, 50)).Position;
                List<string> logged = Logged(() =>
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Is(P(94.666666666666671, 50), Text(db, "ABC", 1, 0, P(100, 50)).Position);
                        Is(unmeasured, Text(db, "中", 1, 0, P(100, 50)).Position);
                    }
                });

                Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("gbcbig.shx"));
                Assert.DoesNotContain(logged, m => m.StartsWith("Warn", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void EveryBigFontCutShort_OrOfABytePutWrong_IsReadOrRefused_AndNeverThrows()
        {
            var random = new Random(20261104);

            foreach (byte[] font in new[] { Chinese(), Japanese(), Symbols() })
            {
                for (int length = 0; length <= font.Length; length++)
                {
                    Measured(font.Take(length).ToArray());
                }

                for (int round = 0; round < 2000; round++)
                {
                    byte[] wrong = (byte[])font.Clone();
                    wrong[random.Next(wrong.Length)] = (byte)random.Next(256);
                    Measured(wrong);
                }
            }

            void Measured(byte[] bytes)
            {
                DxfShxFont read = DxfShxFont.Read(bytes);

                if (read != null)
                {
                    foreach (int number in new[] { 0x20, 0x3F, 0x41, 0xB1, 0x9286, 0x8142, 0xD6D0, 0x7E21, 0xFD01 })
                    {
                        read.GlyphOf(number);
                    }
                }
            }
        }

        /// <summary>The bytes of a font of shapes 1.0 of AutoCAD's form, 6 above and 2 below, its A and B capitals.</summary>
        private static byte[] Shapes()
        {
            var records = new List<KeyValuePair<int, byte[]>>
            {
                new KeyValuePair<int, byte[]>(
                    0,
                    Encoding.ASCII.GetBytes("GH SHAPES").Concat(new byte[] { 0, 6, 2, 0, 0 }).ToArray()
                ),
                new KeyValuePair<int, byte[]>(
                    'A',
                    new byte[] { 0 }
                        .Concat(Capital)
                        .ToArray()
                ),
                new KeyValuePair<int, byte[]>(
                    'B',
                    new byte[] { 0 }
                        .Concat(Capital)
                        .ToArray()
                ),
            };
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A"));
            bytes.AddRange(BitConverter.GetBytes((ushort)0));
            bytes.AddRange(BitConverter.GetBytes((ushort)'B'));
            bytes.AddRange(BitConverter.GetBytes((ushort)records.Count));

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Value.Length));
            }

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                bytes.AddRange(record.Value);
            }

            return bytes.ToArray();
        }
    }
}
