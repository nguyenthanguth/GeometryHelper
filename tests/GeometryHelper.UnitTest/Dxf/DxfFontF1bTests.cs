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
    /// The drawing's SHX fonts (<see cref="DxfDatabase.FontSearchPath"/>, check F1b): fonts written by the tests in AutoCAD's
    /// forms, a unifont and a font of shapes, their letters drawn as txt.shx draws those the tests use (a capital inked 4 by 6
    /// and moving the pen 6, g reaching 2 below the baseline, 1 inked 2 wide and moving it 4), so that the numbers AutoCAD 2021
    /// gave for txt.shx (checks F1a and F1b) are asserted; and the codes of a shape, read as F1a found AutoCAD reads them.
    /// </summary>
    public class DxfFontF1bTests
    {
        private const double Near = 1E-9;

        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        // ---- the fonts ---------------------------------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down from the origin up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The letters the tests use, by their numbers, as txt.shx draws them, and a few of shapes of their own.</summary>
        private static Dictionary<int, byte[]> Letters()
        {
            var letters = new Dictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', new byte[] { 1, 0x64, 0x30, 2, 0x6C, 0x20, 0 } },
                { '1', new byte[] { 1, 0x64, 0x20, 2, 0x6C, 0x20, 0 } },
                { 'g', new byte[] { 1, 0x2C, 0x40, 0x64, 2, 0x4C, 0x20, 0 } },
                { 0x2205, Capital },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            return letters;
        }

        /// <summary>The bytes of a unifont of AutoCAD's form: its header (name, above, below, modes, encoding, embedding), then each letter, its number, length, name and codes.</summary>
        private static byte[] Unifont(int above, int below, IDictionary<int, byte[]> letters)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding
                .ASCII.GetBytes("GH TEST")
                .Concat(new byte[] { 0, (byte)above, (byte)below, 2, 0, 0, 0 })
                .ToArray();
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

        /// <summary>The bytes of a font of shapes 1.0 of AutoCAD's form: the first and last numbers and the count, each shape's number and length, then the shapes, shape 0 the font's name, above, below and modes.</summary>
        private static byte[] Shapes(int above, int below, IDictionary<int, byte[]> letters)
        {
            var records = new List<KeyValuePair<int, byte[]>>
            {
                new KeyValuePair<int, byte[]>(
                    0,
                    Encoding
                        .ASCII.GetBytes("GH SHAPES")
                        .Concat(new byte[] { 0, (byte)above, (byte)below, 0, 0 })
                        .ToArray()
                ),
            };
            records.AddRange(
                letters
                    .Where(l => l.Key < 256)
                    .OrderBy(l => l.Key)
                    .Select(l => new KeyValuePair<int, byte[]>(
                        l.Key,
                        new byte[] { 0 }
                            .Concat(l.Value)
                            .ToArray()
                    ))
            );
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A"));
            bytes.AddRange(BitConverter.GetBytes((ushort)0));
            bytes.AddRange(BitConverter.GetBytes((ushort)records.Max(r => r.Key)));
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

        /// <summary>A folder of its own holding txt.shx, the unifont of <see cref="Letters"/>, 6 above and 2 below as txt.shx is, and the files given; deleted after the test.</summary>
        private static void InFolder(Action<string> test, params KeyValuePair<string, byte[]>[] files)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont(6, 2, Letters()));

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

        /// <summary>A new drawing of R2018, its Standard of txt, its fonts looked for in the folder given.</summary>
        private static DxfDatabase Drawing(string folder)
        {
            var db = new DxfDatabase("AC1032");
            db.FontSearchPath = folder;
            return db;
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Is(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(expected.Subtract(actual).Length <= Near, "expected " + expected + ", got " + actual);

        private static void Is(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= Near,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

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

        /// <summary>A TEXT in model space made in code, in the style Standard, 4 high, its modes and second point given.</summary>
        private static DxfDBText Text(
            DxfDatabase db,
            string words,
            int horizontal,
            int vertical,
            GeoPoint3 second,
            double widthFactor = 1.0,
            double oblique = 0.0,
            string style = "Standard"
        )
        {
            var text = new DxfDBText
            {
                TextString = words,
                Height = 4,
                WidthFactor = widthFactor,
                Oblique = oblique * Math.PI / 180.0,
                TextStyleName = style,
            };
            text.HorizontalMode = (DxfTextHorizontalMode)horizontal;
            text.VerticalMode = (DxfTextVerticalMode)vertical;
            text.AlignmentPoint = second;
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        // ---- the empty path and the faults of a font ----------------------------------------------------------------------------

        /// <summary>The edits of the measured kinds on a drawing, as the tests below make them; the drawing's text after them.</summary>
        private static string Edited(DxfDatabase db)
        {
            DxfDBText text = Text(db, "ABC", 1, 0, P(100, 50));
            text.TextString = "AB";
            text.Height = 8;
            var mtext = new DxfMText
            {
                Contents = "ABC",
                TextHeight = 4,
                Location = P(0, 0),
            };
            db.ModelSpace.AppendEntity(mtext);
            Assert.Equal(0.0, mtext.ActualWidth);
            return db.ToText();
        }

        [Fact]
        public void NoFontSearchPath_ReadsNoFont_LogsNothing_AndWritesTheDrawingAsBefore()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032");
                Assert.Equal(string.Empty, db.FontSearchPath);
                string text = null;
                List<string> logged = Logged(() => text = Edited(db));
                Assert.DoesNotContain(logged, m => m.Contains("font"));

                // Set and set back to empty: the fonts are dropped, the same edits written to the byte as with none.
                var again = new DxfDatabase("AC1032");
                again.FontSearchPath = folder;
                again.FontSearchPath = string.Empty;
                Assert.Equal(text, Edited(again));
                Assert.Throws<ArgumentNullException>(() => db.FontSearchPath = null);
            });
        }

        [Fact]
        public void AFontInNoFolder_IsSaidOnceAtDebug_AndItsTextsAreLaidOutAsWithNoFonts()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx" });
                db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ARIAL", FileName = "arial.ttf" });
                var none = new DxfDatabase("AC1032");
                GeoPoint3 unmeasured = Text(none, "ABC", 1, 0, P(100, 50)).Position;
                List<string> logged = Logged(() =>
                {
                    foreach (string style in new[] { "ROMANS", "ROMANS", "ARIAL", "ARIAL" })
                    {
                        Is(unmeasured, Text(db, "ABC", 1, 0, P(100, 50), style: style).Position);
                    }
                });

                Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("romans.shx"));
                Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("arial.ttf"));
                Assert.DoesNotContain(logged, m => m.StartsWith("Warn", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void AFileFoundThatIsNoFont_IsSaidOnceAtWarn_AndNothingThrows()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "BAD", FileName = "bad" });
                    GeoPoint3 unmeasured = Text(new DxfDatabase("AC1032"), "ABC", 1, 0, P(100, 50)).Position;
                    List<string> logged = Logged(() =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            Is(unmeasured, Text(db, "ABC", 1, 0, P(100, 50), style: "BAD").Position);
                        }
                    });

                    Assert.Single(logged, m => m.StartsWith("Warn", StringComparison.Ordinal) && m.Contains("bad.shx"));
                },
                F("bad.shx", Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A\u0001"))
            );
        }

        [Fact]
        public void EveryFontCutShort_OrOfABytePutWrong_IsReadOrRefused_AndNeverThrows()
        {
            byte[] whole = Unifont(6, 2, Letters());
            byte[] shapes = Shapes(6, 2, Letters());
            var random = new Random(20261103);

            foreach (byte[] font in new[] { whole, shapes })
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

                foreach (int number in new[] { ' ', 'A', 'g', '1', '?', 0x2205 })
                {
                    read?.GlyphOf(number);
                }
            }
        }

        // ---- the codes of a shape, as AutoCAD reads them (check F1a) -------------------------------------------------------------

        private static DxfShxFont.Glyph Shape(params byte[] codes) =>
            DxfShxFont
                .Read(Unifont(6, 2, new Dictionary<int, byte[]> { { 'X', codes }, { 'A', Capital } }))
                .GlyphOf('X');

        private static void Boxed(DxfShxFont.Glyph glyph, double left, double bottom, double right, double top)
        {
            Assert.True(glyph.HasInk);
            Is(left, glyph.Xs.Min());
            Is(bottom, glyph.Ys.Min());
            Is(right, glyph.Xs.Max());
            Is(top, glyph.Ys.Max());
        }

        [Fact]
        public void ACapital_IsInkedFourBySixAndMovesThePenSix_AndAShapeWithin_IsNumberedBigEndian()
        {
            Boxed(Shape(Capital), 0, 0, 4, 6);
            Is(6, Shape(Capital).Advance);

            // 7 then 0x0041, big-endian, draws A; little-endian it would be 0x4100, which the font has not.
            DxfShxFont.Glyph within = Shape(7, 0x00, 0x41, 0);
            Boxed(within, 0, 0, 4, 6);
            Is(6, within.Advance);
        }

        [Fact]
        public void ThePenPutDown_InksThePointItStandsOn_AsAFontMarksTheRightOfALetter()
        {
            // Up, to (3, 0), down and up again, to (5, 0): ink at (3, 0) alone, as romans marks the right of its Cyrillic letters.
            DxfShxFont.Glyph dot = Shape(2, 8, 3, 0, 1, 2, 8, 2, 0, 0);
            Boxed(dot, 3, 0, 3, 0);
            Is(5, dot.Advance);
        }

        [Fact]
        public void AnArc_IsBoxedAsAutoCadBoxesIt_ByItsChordAndHeightUpToHalfATurn_ByItsCircleBeyond()
        {
            // A quarter of radius 4 clockwise from (4, 0), 270 to 180 degrees (octants 0xE2): its chord and the chord moved by its
            // height reach (-0.828, -0.828), where the arc itself reaches (0, 0), as AutoCAD boxes isocp's "l".
            DxfShxFont.Glyph quarter = Shape(2, 8, 4, 0, 1, 10, 4, 0xE2, 2, 8, 6, 0xFC, 0);
            Boxed(quarter, 2 - (4 * Math.Sqrt(0.5)), 2 - (4 * Math.Sqrt(0.5)), 4, 4);

            // Half a circle of radius 2 from (4, 2) round to (0, 2): its box, 0 to 4 by 2 to 4.
            Boxed(Shape(2, 8, 4, 2, 1, 10, 2, 0x04, 2, 0), 0, 2, 4, 4);

            // Five octants from the top, (2, 4), round to 315 degrees: the whole circle's box, x to 4 where the arc reaches 3.414.
            Boxed(Shape(2, 8, 2, 4, 1, 10, 2, 0x25, 0), 0, 0, 4, 4);

            // A whole circle (no count) from (4, 2).
            Boxed(Shape(2, 8, 4, 2, 1, 10, 2, 0x00, 0), 0, 0, 4, 4);
        }

        [Fact]
        public void TheOctantsOfAnArc_AreOfASignAndASize()
        {
            // 0xA4: clockwise, from octant 2, 4 octants: from (2, 4) about (2, 2) round the right side to (2, 0), x 2 to 4; read
            // as two's complement it would be from octant 5 for 12.
            DxfShxFont.Glyph half = Shape(2, 8, 2, 4, 1, 10, 2, 0xA4, 0);
            Boxed(half, 2, 0, 4, 4);
            Is(2, half.Advance);
        }

        [Fact]
        public void AnArcOfFractions_RunsBetweenWholeDegrees()
        {
            // From octant 0 plus 128/256 of one, 22.5 degrees, to the end of it, 45, radius 100, counter-clockwise: 22.5 is taken
            // as 22, as AutoCAD boxes bold's comma at 37 degrees and not 36.914.
            DxfShxFont.Glyph arc = Shape(2, 8, 100, 0, 1, 11, 128, 0, 0, 100, 0x01, 0);
            double Ends(double start) => 100 - (100 * Math.Cos(start * Math.PI / 180)) + (100 * Math.Cos(Math.PI / 4));
            Is(Ends(22), arc.Advance);
            Assert.True(Math.Abs(Ends(22.5) - arc.Advance) > 0.1);
        }

        [Fact]
        public void ShapesWithinEachOther_StopAtTheirDepth_AndAShapeOfNoFont_IsNoFont()
        {
            var cycle = new Dictionary<int, byte[]>
            {
                { 'X', new byte[] { 7, 0, (byte)'Y', 0 } },
                { 'Y', new byte[] { 7, 0, (byte)'X', 0 } },
            };
            DxfShxFont font = DxfShxFont.Read(Unifont(6, 2, cycle));
            Assert.False(font.GlyphOf('X').HasInk);
            Is(0, font.GlyphOf('X').Advance);

            // A file of shapes with no shape 0, as AutoCAD's ltypeshp.shx, has no height: no font of letters.
            Assert.Null(
                DxfShxFont.Read(
                    Encoding
                        .ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A")
                        .Concat(new byte[] { 132, 0, 132, 0, 1, 0, 132, 0, 4, 0, (byte)'T', 0, 2, 0 })
                        .ToArray()
                )
            );
            Assert.Null(DxfShxFont.Read(Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0\r\n\u001A")));
        }

        // ---- TEXT: where it stands, its box (check F1a) -------------------------------------------------------------------------

        [Fact]
        public void ATextOfEachJustification_StandsAboutItsSecondPoint_AsAutoCadPutsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021, txt.shx "ABC" 4 high about the origin (text.tsv): its ink from 0 to 10.667 and 0 to 4.
                var expected = new Dictionary<int[], GeoPoint3>
                {
                    { new[] { 1, 0 }, P(-5.333333333333333, 0) },
                    { new[] { 2, 0 }, P(-10.666666666666666, 0) },
                    { new[] { 4, 0 }, P(-5.333333333333333, -2) },
                    { new[] { 0, 3 }, P(0, -4) },
                    { new[] { 1, 3 }, P(-5.333333333333333, -4) },
                    { new[] { 2, 3 }, P(-10.666666666666666, -4) },
                    { new[] { 0, 2 }, P(0, -2) },
                    { new[] { 1, 2 }, P(-5.333333333333333, -2) },
                    { new[] { 2, 2 }, P(-10.666666666666666, -2) },
                    { new[] { 0, 1 }, P(0, 1.3333333333333333) },
                    { new[] { 1, 1 }, P(-5.333333333333333, 1.3333333333333333) },
                    { new[] { 2, 1 }, P(-10.666666666666666, 1.3333333333333333) },
                };

                foreach (KeyValuePair<int[], GeoPoint3> mode in expected)
                {
                    DxfDBText text = Text(db, "ABC", mode.Key[0], mode.Key[1], P(0, 0));
                    Is(mode.Value, text.Position);
                    Is(P(0, 0), text.AlignmentPoint);
                }

                // Its last letter a space, the box runs from the start of the baseline to the last pen: "A " centred on -4, and
                // " A " on -6, though its ink is from 1 to 6.667 of it; a space before alone counts for nothing.
                Is(P(-4, 0), Text(db, "A ", 1, 0, P(0, 0)).Position);
                Is(P(-6, 0), Text(db, " A ", 1, 0, P(0, 0)).Position);
                Is(P(-8, 0), Text(db, "A ", 2, 0, P(0, 0)).Position);
                Is(P(-4, -2), Text(db, "A ", 4, 0, P(0, 0)).Position);
            });
        }

        [Fact]
        public void AnAlignedTextTakesItsHeight_AndAFittedOneItsWidth_FromItsTwoPoints()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021: "ABC" aligned on (0, 0) and (30, 0) is 11.25 high; fitted, 2.8125 wide; "A " aligned 15 high.
                var aligned = new DxfDBText
                {
                    TextString = "ABC",
                    Height = 4,
                    HorizontalMode = DxfTextHorizontalMode.TextAlign,
                };
                aligned.Position = P(0, 0);
                aligned.AlignmentPoint = P(30, 0);
                db.ModelSpace.AppendEntity(aligned);
                Is(11.25, aligned.Height);
                Is(P(0, 0), aligned.Position);

                var fitted = new DxfDBText
                {
                    TextString = "ABC",
                    Height = 4,
                    HorizontalMode = DxfTextHorizontalMode.TextFit,
                };
                fitted.Position = P(0, 0);
                fitted.AlignmentPoint = P(30, 0);
                db.ModelSpace.AppendEntity(fitted);
                Is(2.8125, fitted.WidthFactor);
                Is(4, fitted.Height);

                var spaced = new DxfDBText
                {
                    TextString = "A ",
                    Height = 4,
                    HorizontalMode = DxfTextHorizontalMode.TextAlign,
                };
                spaced.Position = P(0, 0);
                spaced.AlignmentPoint = P(30, 0);
                db.ModelSpace.AppendEntity(spaced);
                Is(15, spaced.Height);

                // The first point set again takes the height anew: 15 apart, 5.625 high.
                aligned.Position = P(15, 0);
                Is(5.625, aligned.Height);
            });
        }

        [Fact]
        public void ATextSetAgain_StandsAnewAboutItsSecondPoint()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                DxfDBText text = Text(db, "ABC", 1, 0, P(100, 50));
                Is(P(94.666666666666671, 50), text.Position);

                text.TextString = "AB";
                Is(P(100 - (10.0 / 3.0), 50), text.Position);
                text.Height = 8;
                Is(P(100 - (20.0 / 3.0), 50), text.Position);
                text.WidthFactor = 0.5;
                Is(P(100 - (10.0 / 3.0), 50), text.Position);
                text.Rotation = Math.PI / 2;
                Is(P(100, 50 - (10.0 / 3.0)), text.Position);
                text.Rotation = 0;
                text.Justify = DxfAttachmentPoint.TopRight;
                Is(P(100 - (20.0 / 3.0), 42), text.Position);
                text.AlignmentPoint = P(0, 0);
                Is(P(-20.0 / 3.0, -8), text.Position);

                // The value it has changes nothing; the oblique never moves the text.
                text.AlignmentPoint = P(0, 0);
                text.Oblique = 15 * Math.PI / 180;
                Is(P(-20.0 / 3.0, -8), text.Position);
            });
        }

        [Fact]
        public void TheExtentsOfAText_AreTheInkOfItsWordsSlantedWhole()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021: "A" 4 high at the origin, 0.7 wide and slanted 15 degrees, from (0, 0) to (2.938, 4); "ABC" slanted 15
                // to 11.738; " A" from 4, its space no ink; "g" down to -1.333.
                GeoAabb3 a = Text(db, "A", 0, 0, P(0, 0), 0.7, 15).GeometricExtents;
                Is(P(0, 0), a.Min);
                Is(new GeoPoint3(2.9384634363911575, 4, 0), a.Max);
                Is(11.738463436391157, Text(db, "ABC", 0, 0, P(0, 0), 1, 15).GeometricExtents.Max.X);
                Is(4, Text(db, " A", 0, 0, P(0, 0)).GeometricExtents.Min.X);
                Is(-4.0 / 3.0, Text(db, "g", 0, 0, P(0, 0)).GeometricExtents.Min.Y);

                // %%c is the diameter sign U+2205 of a unifont; a letter the font has not is drawn as its question mark.
                Is(2.6666666666666665, Text(db, "%%c", 0, 0, P(0, 0)).GeometricExtents.Max.X);
                Is(2, Text(db, "中", 0, 0, P(0, 0)).GeometricExtents.Max.X);
            });
        }

        [Fact]
        public void AFontOfShapes_IsMeasuredByItsBytes_ItsSignsAt127To129()
        {
            Dictionary<int, byte[]> letters = Letters();
            letters.Add(129, new byte[] { 1, 0x64, 0x50, 2, 0x6C, 0x20, 0 });
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "SHAPES", FileName = "gh-shapes.shx" });
                    DxfDBText text = Text(db, "%%cA", 0, 0, P(0, 0));
                    text.TextStyleName = "SHAPES";
                    Is(11 * 4.0 / 6.0, text.GeometricExtents.Max.X);
                },
                F("gh-shapes.shx", Shapes(6, 2, letters))
            );
        }

        // ---- MTEXT of one line (check F1a) ---------------------------------------------------------------------------------------

        [Fact]
        public void AnMTextOfOneLine_IsAsWideAndHighAsAutoCadMeasuresIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                DxfMText Words(string contents, double width = 0)
                {
                    var mtext = new DxfMText
                    {
                        Contents = contents,
                        TextHeight = 4,
                        Width = width,
                        Location = P(0, 0),
                    };
                    db.ModelSpace.AppendEntity(mtext);
                    return mtext;
                }

                // AutoCAD 2021, txt.shx (mtext.tsv, mtext2.tsv).
                Is(10.666666666666666, Words("ABC").ActualWidth);
                Is(4, Words("ABC").ActualHeight);
                Is(10.666666666666666, Words("ABC   ").ActualWidth);
                Is(22.666666666666664, Words("   ABC").ActualWidth);
                Is(8, Words("  ").ActualWidth);
                Is(7.4666666666666659, Words("\\W0.7;ABC").ActualWidth);
                Is(21.333333333333336, Words("\\H2x;ABC").ActualWidth);
                Is(8, Words("\\H2x;ABC").ActualHeight);
                Is(21.333333333333336, Words("\\H8;ABC").ActualWidth);
                Is(5.333333333333333, Words("g").ActualHeight);
                Is(6.666666666666667, Words("Ag").ActualWidth);

                // Several lines, codes laid out and words that wrap are measured (check F4); a letter the font has not keeps 42.
                Is(10.666666666666666, Words("ABC\\PDE").ActualWidth);
                Is(14.666666666666666, Words("\\T1.5;ABC").ActualWidth);
                Assert.Equal(0.0, Words("A中").ActualWidth);
                Is(10.666666666666666, Words("ABC ABC ABC", 20).ActualWidth);
                Is(10.666666666666666, Words("ABC", 20).ActualWidth);
            });
        }

        // ---- multileaders: words met on the right (checks F1a, F1b, D31b) --------------------------------------------------------

        private const string Right =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        private const string Both =
            "270 2|300 CONTEXT_DATA{|40 1.0|10 98.0|20 48.0|30 0.0|41 4.0|140 4.0|145 2.0|174 1|175 1|176 0|177 0|290 1|304 ABC|11 0.0|21 0.0|31 1.0|340 55|12 100.0|22 50.0|32 0.0|13 1.0|23 0.0|33 0.0|42 0.0|43 0.0|44 0.0|45 1.0|170 1|90 -1073741824|171 1|172 5|91 -1073741824|141 0.0|92 0|291 0|292 0|173 0|293 0|142 0.0|143 0.0|294 0|295 0|296 0|110 0.0|120 0.0|130 0.0|111 1.0|121 0.0|131 0.0|112 0.0|122 1.0|132 0.0|297 0|302 LEADER{|290 1|291 1|10 90.0|20 48.0|30 0.0|11 1.0|21 0.0|31 0.0|90 0|40 8.0|304 LEADER_LINE{|10 0.0|20 0.0|30 0.0|91 0|305 }|271 0|303 }|302 LEADER{|290 1|291 1|10 120.6666666666666|20 48.0|30 0.0|11 -1.0|21 0.0|31 0.0|90 1|40 8.0|304 LEADER_LINE{|10 250.0|20 0.0|30 0.0|91 1|305 }|271 0|303 }|272 9|273 9|301 }|340 67|90 295936|170 1|91 -1056964608|341 5B|171 -2|290 1|291 1|41 8.0|42 4.0|172 2|343 55|173 1|95 1|174 1|175 0|92 -1073741824|292 0|93 -1056964608|10 1.0|20 1.0|30 1.0|43 0.0|176 0|293 0|294 0|178 0|179 1|45 1.0|271 0|272 9|273 9|295 1";

        /// <summary>A drawing of a Standard multileader style, its fonts looked for in the folder given.</summary>
        private static DxfDatabase Leaders(string folder)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.FontSearchPath = folder;
            return db;
        }

        /// <summary>A MULTILEADER of AutoCAD 2021's own record, as D31b's tests make one: its groups after AcDbMLeader those given, the drawing's own style and text style put in.</summary>
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

        /// <summary>A multileader made in code as F1a's plugin made AutoCAD's: words 4 high at (100, 50) on the Standard of a gap of 2 and doglegs of 8, a line added from the point given.</summary>
        private static DxfMLeader Made(DxfDatabase db, string words, GeoPoint3 from, int attachment = 1)
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
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.LeftLeader);
            mleader.SetTextAttachmentType((DxfTextAttachmentType)attachment, DxfLeaderDirectionType.RightLeader);
            mleader.TextLocation = P(100, 50);
            mleader.AddLeaderLine(from);
            return mleader;
        }

        [Fact]
        public void ALeaderAddedOnTheRightOfWords_LandsAsFarRightAsTheirInk_AsAutoCadLandsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D31's d02, F1a's ml.txt): "ABC" 10.67 wide at (100, 50) met from (250, 0), the dogleg from
                // (120.67, 48) against x; "A" from (112.67, 48); "  ABC", its spaces counted, from (128.67, 48).
                DxfMLeader right = Made(db, "ABC", P(250, 0));
                Is(P(120.66666666666667, 48), right.GetLastVertex(0));
                Is(new GeoVector3(-1, 0, 0), right.GetDogleg(0));
                Is(P(100, 50), right.TextLocation);
                Is(P(112.66666666666667, 48), Made(db, "A", P(250, 0)).GetLastVertex(0));
                Is(P(128.66666666666666, 48), Made(db, "  ABC", P(250, 0)).GetLastVertex(0));
                Is(P(120.66666666666667, 48), Made(db, "ABC  ", P(250, 0)).GetLastVertex(0));

                // The side is that of the middle of the words: under words from 100 to 110.67, 103 lands left and 108 right.
                Is(P(90, 48), Made(db, "ABC", P(103, 0)).GetLastVertex(0));
                Is(P(120.66666666666667, 48), Made(db, "ABC", P(108, 0)).GetLastVertex(0));
            });
        }

        [Fact]
        public void WordsReachingBelowTheirBaseline_AreMetLower_AsAutoCadMeetsThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (F1b, txt.shx): "Ag", its g 1.333 below the baseline, met 2.667 below its top at the middle of its top
                // line, its middle and the middle of the bottom line; 5.333 at the bottom and under it; 0 at the top.
                var below = new Dictionary<int, double>
                {
                    { 0, 0 },
                    { 1, 8.0 / 3.0 },
                    { 2, 8.0 / 3.0 },
                    { 3, 8.0 / 3.0 },
                    { 4, 16.0 / 3.0 },
                    { 5, 16.0 / 3.0 },
                    { 6, 16.0 / 3.0 },
                    { 7, 16.0 / 3.0 },
                };

                foreach (KeyValuePair<int, double> attachment in below)
                {
                    Is(P(90, 50 - attachment.Value), Made(db, "Ag", P(0, 0), attachment.Key).GetLastVertex(0));
                    Is(
                        P(116.66666666666667, 50 - attachment.Value),
                        Made(db, "Ag", P(250, 0), attachment.Key).GetLastVertex(0)
                    );
                }

                // "ABC" of no ink below, 2 at the middle of the top line, as with no fonts.
                Is(P(90, 48), Made(db, "ABC", P(0, 0)).GetLastVertex(0));
            });
        }

        [Fact]
        public void WordsMetOnTheRight_MoveToTheirLanding_WhenTheirHeightOrScaleChanges_AsAutoCadMovesThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (D31b m-right-height10, m-right-scale2): met on the right alone, 10 high, at (84, 53); at a scale of
                // 2, at (79.33, 52); the dogleg kept from (120.67, 48).
                DxfMLeader high = AutoCads(db, Right);
                high.TextHeight = 10;
                Is(P(84, 53), high.TextLocation);
                Is(P(120.66666666666667, 48), high.GetLastVertex(0));

                DxfMLeader scaled = AutoCads(db, Right);
                scaled.Scale = 2;
                Is(P(79.333333333333343, 52), scaled.TextLocation);
                Is(P(120.66666666666667, 48), scaled.GetLastVertex(0));

                // Met on both sides (m2-both-height10, m2-both-scale2): the words as on the left, the right landed anew at them.
                DxfMLeader both = AutoCads(db, Both);
                both.TextHeight = 10;
                Is(P(100, 53), both.TextLocation);
                Is(P(90, 48), both.GetLastVertex(0));
                Is(P(136.66666666666666, 48), both.GetLastVertex(1));

                DxfMLeader bothScaled = AutoCads(db, Both);
                bothScaled.Scale = 2;
                Is(P(102, 52), bothScaled.TextLocation);
                Is(P(82, 48), bothScaled.GetLastVertex(0));
                Is(P(143.33333333333331, 48), bothScaled.GetLastVertex(1));
            });
        }

        [Fact]
        public void WordsGivenAnewOfAnotherWidth_KeepTheirLandingOnTheRight_AsAutoCadKeepsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (F1b): "ABCDEF" given to words met on the right alone stand at (88, 50), "AB" at (104, 50); met on both
                // sides they stay and the right leader lands anew, from (132.67, 48) and (116.67, 48).
                foreach (
                    KeyValuePair<string, double[]> anew in new Dictionary<string, double[]>
                    {
                        { "ABCDEF", new[] { 88, 132.66666666666666 } },
                        { "AB", new[] { 104, 116.66666666666667 } },
                    }
                )
                {
                    DxfMLeader right = AutoCads(db, Right);
                    DxfMText words = right.MText;
                    words.Contents = anew.Key;
                    right.MText = words;
                    Is(P(anew.Value[0], 50), right.TextLocation);
                    Is(P(120.66666666666667, 48), right.GetLastVertex(0));

                    DxfMLeader both = AutoCads(db, Both);
                    words = both.MText;
                    words.Contents = anew.Key;
                    both.MText = words;
                    Is(P(100, 50), both.TextLocation);
                    Is(P(90, 48), both.GetLastVertex(0));
                    Is(P(anew.Value[1], 48), both.GetLastVertex(1));
                }
            });
        }

        [Fact]
        public void MirroredWords_AreKeptReadable_AsAutoCadKeepsThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);
                GeoTransform3 mirrorXY = GeoTransform3.Mirror(
                    new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1))
                );
                GeoTransform3 mirrorYZ = GeoTransform3.Mirror(
                    new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0))
                );

                // AutoCAD 2021 (D31e e5-words-mirrorxy, e5-words-right-mirrorxy): across XY, "ABC" held at (110.67, 50, 0) facing
                // down and running against x, the leaders kept.
                foreach (GeoPoint3 from in new[] { P(0, 0), P(250, 0) })
                {
                    DxfMLeader words = Made(db, "ABC", from);
                    words.TransformBy(mirrorXY);
                    DxfMText mirrored = words.MText;
                    Is(P(110.66666666666667, 50), words.TextLocation);
                    Is(new GeoVector3(-1, 0, 0), mirrored.Direction);
                    Is(new GeoVector3(0, 0, -1), mirrored.Normal);
                    Assert.Equal(DxfAttachmentPoint.TopLeft, mirrored.Attachment);
                    Is(from.X == 0 ? P(90, 48) : P(120.66666666666667, 48), words.GetLastVertex(0));
                }

                // Across YZ (g-words-mirroryz): held by their top right at (-100, 50, 0), running along x, the leader on their right.
                DxfMLeader across = Made(db, "ABC", P(0, 0));
                across.TransformBy(mirrorYZ);
                Is(P(-100, 50), across.TextLocation);
                Is(new GeoVector3(1, 0, 0), across.MText.Direction);
                Assert.Equal(DxfAttachmentPoint.TopRight, across.MText.Attachment);
                Is(P(-90, 48), across.GetLastVertex(0));
                Is(new GeoVector3(-1, 0, 0), across.GetDogleg(0));
            });
        }

        // ---- attribute definitions in a flip (check F1b) -------------------------------------------------------------------------

        [Fact]
        public void AnAttributeDefinitionInAFlip_IsKeptReadable_AsAutoCadKeepsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                var block = new DxfBlockTableRecord { Name = "GH_FLIP", Origin = new GeoPoint3(0, 0, 0) };
                db.BlockTable.Add(block);
                var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 90, 0));
                block.AppendEntity(line);
                var left = new DxfAttributeDefinition(P(20, 60), "L1", "LEFT", "Left", null)
                {
                    Height = 5,
                    LockPositionInBlock = true,
                };
                block.AppendEntity(left);
                var mid = new DxfAttributeDefinition(P(20, 40), "M1", "MID", "Mid", null)
                {
                    Height = 5,
                    LockPositionInBlock = true,
                };
                block.AppendEntity(mid);
                mid.Justify = DxfAttachmentPoint.MiddleCenter;
                mid.AlignmentPoint = P(20, 40);
                var right = new DxfAttributeDefinition(P(20, 20), "Ag1", "RIGHT", "Right", null)
                {
                    Height = 5,
                    LockPositionInBlock = true,
                };
                block.AppendEntity(right);
                right.Justify = DxfAttachmentPoint.BaseRight;
                right.AlignmentPoint = P(20, 20);
                DxfBlockFlipParameter hand = block.AddFlipParameter(
                    "Hand",
                    null,
                    new GeoPoint3(45, 0, 0),
                    new GeoPoint3(45, 50, 0)
                );
                block.AddFlipAction(hand, new DxfEntity[] { line, left, mid, right });
                var insert = new DxfBlockReference(new GeoPoint3(500, 0, 0), block)
                {
                    Rotation = Math.PI / 2,
                    ScaleFactors = new GeoVector3(2, 2, 2),
                };
                db.ModelSpace.AppendEntity(insert);

                foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>().ToList())
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                    insert.AttributeCollection.AppendAttribute(attribute);
                }

                // AutoCAD 2021 (F1b flip.txt, txt.shx): flipped about x = 45, LEFT at (63.33, 60), MID at (66.67, 37.5) about
                // (70, 40), RIGHT at (70, 20) about (81.67, 20), each INSERT's ATTRIB moved as much in its turn and scale.
                DxfDatabase back = DxfDatabase.Parse(db.ToText());
                back.FontSearchPath = folder;
                DxfBlockReference flipped = back.ModelSpace.OfType<DxfBlockReference>().Single();
                flipped.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Hand").Value = (short)1;
                Dictionary<string, DxfAttributeDefinition> shown = flipped
                    .BlockTableRecord.OfType<DxfAttributeDefinition>()
                    .ToDictionary(d => d.Tag);
                Is(P(63.333333333333336, 60), shown["LEFT"].Position);
                Is(P(66.666666666666657, 37.5), shown["MID"].Position);
                Is(P(69.999999999999986, 40), shown["MID"].AlignmentPoint);
                Is(P(70, 20), shown["RIGHT"].Position);
                Is(P(81.666666666666671, 20), shown["RIGHT"].AlignmentPoint);
                Dictionary<string, DxfAttributeReference> attributes = flipped
                    .AttributeCollection.Cast<DxfAttributeReference>()
                    .ToDictionary(a => a.Tag);
                Is(P(380, 126.66666666666669), attributes["LEFT"].Position);
                Is(P(460, 140), attributes["RIGHT"].Position);
            });
        }

        [Fact]
        public void AnAttributeDefinitionInAFlip_IsStillRefused_WhereNoFontMeasuresIt()
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_FLIP", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var definition = new DxfAttributeDefinition(P(20, 60), "L1", "LEFT", "Left", null)
            {
                Height = 5,
                LockPositionInBlock = true,
            };
            block.AppendEntity(definition);
            DxfBlockFlipParameter hand = block.AddFlipParameter(
                "Hand",
                null,
                new GeoPoint3(45, 0, 0),
                new GeoPoint3(45, 50, 0)
            );
            Assert.Throws<ArgumentException>(() => block.AddFlipAction(hand, new DxfEntity[] { definition }));
        }

        // ---- a leader exploded (check F1b) ---------------------------------------------------------------------------------------

        [Fact]
        public void ALeaderOfWordsAboveItsLine_ExplodesItsHookOnToTheirFarEnd_AsAutoCadExplodesIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                var words = new DxfMText
                {
                    Contents = "ABC",
                    TextHeight = 2.5,
                    Attachment = DxfAttachmentPoint.MiddleLeft,
                    Location = P(22, 10),
                };
                db.ModelSpace.AppendEntity(words);
                var leader = new DxfLeader { HasArrowHead = true };
                leader.AppendVertex(P(0, 0));
                leader.AppendVertex(P(18.875, 8.125));
                leader.AppendVertex(P(21.375, 8.125));
                leader.Dimasz = 2.5;
                leader.Dimgap = 0.625;
                leader.Dimtad = 1;
                db.ModelSpace.AppendEntity(leader);
                leader.Annotation = words;

                // Of a hook (75 of 1), as AutoCAD 2021 writes the leader of words it lands at their side.
                List<KeyValuePair<int, string>> groups = leader
                    .Item.File.TailOf(leader.Item, 0)
                    .Select(g => new KeyValuePair<int, string>(g.TypeCode, g.TypeCode == 75 ? "1" : g.Value))
                    .ToList();
                leader.Item.File.ReplaceTail(leader.Item, 0, groups);
                Assert.True(leader.HasHookLine);

                // AutoCAD 2021 (F1b leader2.txt): the polyline from the arrow's back through the hook's start to (28.67, 8.125), the
                // far end of "ABC" 6.67 wide held at (22, 10); with the words beside the line (Dimtad 0) it ends at the hook.
                DxfPolyline line = Assert.Single(leader.Explode().OfType<DxfPolyline>());
                Assert.Equal(3, line.NumberOfVertices);
                Is(2.2962864850452811, line.GetPoint2dAt(0).X);
                Is(0.98846769223803488, line.GetPoint2dAt(0).Y);
                Is(18.875, line.GetPoint2dAt(1).X);
                Is(28.666666666666664, line.GetPoint2dAt(2).X);
                Is(8.125, line.GetPoint2dAt(2).Y);

                leader.Dimtad = 0;
                Is(21.375, Assert.Single(leader.Explode().OfType<DxfPolyline>()).GetPoint2dAt(2).X);
            });
        }
    }
}
