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
    /// The drawing's TrueType fonts (<see cref="DxfDatabase.FontSearchPath"/>, check F2): fonts written by the tests
    /// (<see cref="TrueTypeFile"/>), an em of 1000 units and an A 700 high, so that the rules AutoCAD 2021 measures TrueType
    /// words by (checks F1a and F2) are asserted: the height the top of A, no kerning, the box of a TEXT from the start of its
    /// baseline to its last pen, its bottom on the font's usWinDescent, %%c the letter Ø where the font has no diameter sign.
    /// </summary>
    public class DxfFontF2Tests
    {
        private const double Near = 1E-9;

        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        /// <summary>A folder of its own holding gh.ttf, the font of <see cref="TrueTypeFile.Letters"/>, and the files given; deleted after the test.</summary>
        private static void InFolder(Action<string> test, params KeyValuePair<string, byte[]>[] files)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-truetype-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "gh.ttf"), TrueTypeFile.Font(TrueTypeFile.Letters()));

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

        private static KeyValuePair<string, byte[]> F(string name, byte[] bytes) => new KeyValuePair<string, byte[]>(name, bytes);

        /// <summary>A style of a font's file, and of a typeface where one is given, added to a drawing.</summary>
        private static void Style(DxfDatabase db, string name, string file, string typeFace = null, bool bold = false, bool italic = false)
        {
            var style = new DxfTextStyleTableRecord { Name = name, FileName = file };
            db.TextStyleTable.Add(style);

            if (typeFace != null)
            {
                style.Font = new DxfFontDescriptor(typeFace, bold, italic, 0, 34);
            }
        }

        /// <summary>A new drawing of R2018, its style GH of gh.ttf, its fonts looked for in the folder given.</summary>
        private static DxfDatabase Drawing(string folder)
        {
            var db = new DxfDatabase("AC1032");
            Style(db, "GH", "gh.ttf");
            db.FontSearchPath = folder;
            return db;
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        private static void Is(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= Near, "expected " + expected.ToString("R", CultureInfo.InvariantCulture) + ", got " + actual.ToString("R", CultureInfo.InvariantCulture));

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

        /// <summary>A TEXT in model space made in code, 7 high (the top of A), its modes and second point given.</summary>
        private static DxfDBText Text(DxfDatabase db, string words, int horizontal, int vertical, GeoPoint3 second, double widthFactor = 1.0, string style = "GH")
        {
            var text = new DxfDBText { TextString = words, Height = 7, WidthFactor = widthFactor, TextStyleName = style };
            text.HorizontalMode = (DxfTextHorizontalMode)horizontal;
            text.VerticalMode = (DxfTextVerticalMode)vertical;
            text.AlignmentPoint = second;
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        /// <summary>Where a TEXT of the words and modes given stands in a drawing of no fonts.</summary>
        private static GeoPoint3 Unmeasured(string words, int horizontal, int vertical, GeoPoint3 second)
        {
            var db = new DxfDatabase("AC1032");
            Style(db, "GH", "gh.ttf");
            return Text(db, words, horizontal, vertical, second).Position;
        }

        // ---- TEXT (checks F1a, F2) ---------------------------------------------------------------------------------------------

        [Fact]
        public void TheHeightOfATrueTypeText_IsTheTopOfItsA_AndItsBoxRunsToItsLastPen()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021 (F1a: Times New Roman's A, 1387 up, not its H or capitals, 1356): 7 high is 0.01 to the unit. "H",
                // inked 0.5 to 6.5 and 7.2 up, is centred on its advance, 0 to 7 (Arial "H" centred on 0 to 4.035, not its ink).
                DxfDBText h = Text(db, "H", 1, 0, P(100, 50));
                Is(P(96.5, 50), h.Position);
                Is(P(96.5, 50), h.GeometricExtents.Min);
                Is(new GeoPoint3(103.5, 57.2, 0), h.GeometricExtents.Max);

                // "A", inked from -0.1 to 6.1 past its advance of 6: its box from -0.1 to 6.1, as Arial's A from -3 units.
                DxfDBText a = Text(db, "A", 2, 0, P(100, 50));
                Is(P(93.9, 50), a.Position);
                Is(P(93.8, 50), a.GeometricExtents.Min);
                Is(new GeoPoint3(100, 57, 0), a.GeometricExtents.Max);

                // At a width factor of 0.7 the box is as much narrower; up, the top and the middle by the height.
                Is(P(97.55, 50), Text(db, "H", 1, 0, P(100, 50), 0.7).Position);
                Is(P(100, 43), Text(db, "A", 0, 3, P(100, 50)).Position);
                Is(P(100, 46.5), Text(db, "A", 0, 2, P(100, 50)).Position);
            });
        }

        [Fact]
        public void APairOfLettersTheFontKerns_IsSetByTheirAdvancesAlone()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021 (F1a): "AV" of Arial is as wide as A's advance and V's, though Arial kerns them by -152 units. The
                // font here kerns them by -100: right-aligned on 100 it stands at 88, its box 12 wide, not at 89.
                Is(P(88, 50), Text(db, "AV", 2, 0, P(100, 50)).Position);
                var mtext = new DxfMText { Contents = "AV", TextHeight = 7, TextStyleName = "GH", Location = P(0, 0) };
                db.ModelSpace.AppendEntity(mtext);
                Is(12, mtext.ActualWidth);
            });
        }

        [Fact]
        public void ATextAtItsBottom_StandsOnTheFontsWinDescent_AndAtTheMiddleOfItsWordsOnTheirInk()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021 (F1a: Calibri's usWinDescent 550, Consolas' 514; the descent of hhea does not fit): 7 times 210 over
                // 700 above the second point.
                Is(P(100, 52.1), Text(db, "A", 0, 1, P(100, 50)).Position);

                // The middle of the words (72 of 4) is that of their box: "Ag" from -2 up to 7, and from -0.1 to its advance of 11.
                Is(P(94.55, 47.5), Text(db, "Ag", 4, 0, P(100, 50)).Position);
            });
        }

        [Fact]
        public void ALetterAboveTheBaseline_IsBoxedFromIt_AndASpaceAUnitBelowIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021 (F2): Arial's degree sign, inked from 936 to 1491 units up, is boxed from 0 to 555. Here, from 500
                // to 700, it is boxed from 0 to 2: held by its middle at (100, 50), it stands at (97.5, 49).
                DxfDBText degree = Text(db, "°", 4, 0, P(100, 50));
                Is(P(97.5, 49), degree.Position);
                Is(P(97.5, 49), degree.GeometricExtents.Min);
                Is(new GeoPoint3(102.5, 51, 0), degree.GeometricExtents.Max);

                // A space, of no outline, is boxed a unit below the baseline, as Windows boxes it (Arial " " to -1 unit).
                DxfDBText space = Text(db, " ", 4, 0, P(100, 50));
                Is(P(98.75, 50.005), space.Position);
                Is(new GeoPoint3(98.75, 49.995, 0), space.GeometricExtents.Min);
            });
        }

        [Fact]
        public void PercentPercentC_IsTheLetterOSlash_ButTheDiameterSignWhereTheFontHasOne()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    Style(db, "SIGN", "gh-sign.ttf");

                    // AutoCAD 2021 (F1a): Arial, which has no U+2205, draws %%c as U+00D8, 1593 wide, not as its stand-in. (F2) isocpeur.ttf,
                    // which has both, draws its U+2205.
                    Is(P(96, 50), Text(db, "%%c", 1, 0, P(100, 50)).Position);
                    Is(P(95.5, 50), Text(db, "%%c", 1, 0, P(100, 50), style: "SIGN").Position);
                    var mtext = new DxfMText { Contents = "%%c", TextHeight = 7, TextStyleName = "GH", Location = P(0, 0) };
                    db.ModelSpace.AppendEntity(mtext);
                    Is(8, mtext.ActualWidth);
                },
                F("gh-sign.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(true))));
        }

        [Fact]
        public void ALetterTheFontHasNot_LeavesTheTextAsWithNoFonts_AndItsLinesOfPercentPercentUAreNotBoxed()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021 (F2): Arial "中" is 5.514 wide in a TEXT, drawn from another font of Windows', not Arial's stand-in.
                Is(Unmeasured("A中", 1, 0, P(100, 50)), Text(db, "A中", 1, 0, P(100, 50)).Position);
                Is(Unmeasured("%%kA", 1, 0, P(100, 50)), Text(db, "%%kA", 1, 0, P(100, 50)).Position);

                // (F1a) Arial "%%u%%oA" is centred as "A" is: the lines of a TrueType TEXT are not in its box.
                Is(P(97, 50), Text(db, "%%u%%oA", 1, 0, P(100, 50)).Position);
            });
        }

        // ---- MTEXT, multileaders, leaders, flips (checks F1a, F2) --------------------------------------------------------------

        [Fact]
        public void AnMTextOfOneLineOfTrueType_IsAsWideAndHighAsAutoCadMeasuresIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                DxfMText Words(string contents)
                {
                    var mtext = new DxfMText { Contents = contents, TextHeight = 7, TextStyleName = "GH", Location = P(0, 0) };
                    db.ModelSpace.AppendEntity(mtext);
                    return mtext;
                }

                // AutoCAD 2021 (F1a, F2): ink left of the line's start is not counted (Arial "A" 3.735 wide at 4 high, from 0); the
                // height is from the baseline or the ink's bottom to the letters' height or the ink's top ("H" 7.2, "g" 9).
                Is(6.1, Words("A").ActualWidth);
                Is(7, Words("A").ActualHeight);
                Is(6, Words("H").ActualWidth);
                Is(7.2, Words("H").ActualHeight);
                Is(9, Words("g").ActualHeight);
                Is(8.6, Words(" A").ActualWidth);
                Is(7, Words(" A").ActualHeight);
                Is(6.1, Words("A ").ActualWidth);
                Is(7, Words("A ").ActualHeight);
                Is(4.27, Words("\\W0.7;A").ActualWidth);

                // A space between letters is boxed a unit below the baseline, those before and after are not (Arial "A B" 4.003).
                Is(14.6, Words("A A").ActualWidth);
                Is(7.01, Words("A A").ActualHeight);

                // A letter boxed from the baseline: the degree sign 2 high (Arial "%%d" 4 high, its ink 4.068 up).
                Is(3, Words("°").ActualWidth);
                Is(7, Words("°").ActualHeight);

                // A change of height after a letter is not measured (Arial "A\H2x;BC" 18.819 wide, from the ink of A on): 42 kept.
                Assert.Equal(0.0, Words("A\\H2x;A").ActualWidth);
            });
        }

        /// <summary>A drawing of a Standard multileader style, its style GH of gh.ttf, its fonts looked for in the folder given.</summary>
        private static DxfDatabase Leaders(string folder)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            Style(db, "GH", "gh.ttf");
            db.FontSearchPath = folder;
            return db;
        }

        /// <summary>A multileader made in code as F1a's plugin made AutoCAD's: words of GH 4 high at (100, 50) on the Standard of a gap of 2 and doglegs of 8, a line added from the point given.</summary>
        private static DxfMLeader Made(DxfDatabase db, string words, GeoPoint3 from, int attachment = 1)
        {
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = words, Location = P(100, 50), TextHeight = 4.0, TextStyleName = "GH" } };
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
        public void ALeaderAddedOnTheRightOfTrueTypeWords_LandsAsFarRightAsTheirInk_AndLowerByTheirDepth()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Leaders(folder);

                // AutoCAD 2021 (F1a, F2: 288 multileaders of Arial, Times New Roman, Calibri and isocpeur.ttf): the landing is the
                // words' width and the gap past their start, "AV" 1200 units wide at 4 over 700 to the unit.
                Is(P(100 + (1200 * 4.0 / 700) + 10, 48), Made(db, "AV", P(250, 0)).GetLastVertex(0));
                Is(P(90, 48), Made(db, "AV", P(0, 0)).GetLastVertex(0));

                // "Ag", its g 200 units below the baseline: met as far below its top as half its height and that depth.
                double depth = 200 * 4.0 / 700;
                Is(P(100 + (1080 * 4.0 / 700) + 10, 50 - ((4 + depth) / 2)), Made(db, "Ag", P(250, 0)).GetLastVertex(0));
            });
        }

        [Fact]
        public void AnAttributeDefinitionOfTrueTypeInAFlip_IsKeptReadable_ItsSpacesAtEitherEndLeftOut()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                var block = new DxfBlockTableRecord { Name = "GH_FLIP", Origin = new GeoPoint3(0, 0, 0) };
                db.BlockTable.Add(block);
                var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 90, 0));
                block.AppendEntity(line);
                var acted = new List<DxfEntity> { line };

                foreach (string words in new[] { "A", "A ", " A" })
                {
                    var definition = new DxfAttributeDefinition(P(20, 10 * acted.Count), words, "T" + acted.Count.ToString(CultureInfo.InvariantCulture), "T", null) { Height = 7, LockPositionInBlock = true, TextStyleName = "GH" };
                    block.AppendEntity(definition);
                    acted.Add(definition);
                }

                DxfBlockFlipParameter hand = block.AddFlipParameter("Hand", null, new GeoPoint3(45, 0, 0), new GeoPoint3(45, 50, 0));
                block.AddFlipAction(hand, acted);
                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));

                // AutoCAD 2021 (F2, 142 of 144 ATTDEFs of Arial, Times New Roman and Calibri): one at the left of its baseline has
                // the box of its ink mirrored, "L1 " and " L1" of Arial as "L1"; here "A", inked from -0.1 to 6.1, at 20 about
                // x = 45 stands at 64.
                DxfDatabase back = DxfDatabase.Parse(db.ToText());
                back.FontSearchPath = folder;
                DxfBlockReference flipped = back.ModelSpace.OfType<DxfBlockReference>().Single();
                flipped.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Hand").Value = (short)1;

                foreach (DxfAttributeDefinition definition in flipped.BlockTableRecord.OfType<DxfAttributeDefinition>())
                {
                    Is(64, definition.Position.X);
                }
            });
        }

        [Fact]
        public void ALeaderOfTrueTypeWordsAboveItsLine_ExplodesItsHookOnToTheirFarEnd()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);
                var words = new DxfMText { Contents = "AV", TextHeight = 2.5, TextStyleName = "GH", Attachment = DxfAttachmentPoint.MiddleLeft, Location = P(22, 10) };
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
                List<KeyValuePair<int, string>> groups = leader.Item.File.TailOf(leader.Item, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.TypeCode == 75 ? "1" : g.Value)).ToList();
                leader.Item.File.ReplaceTail(leader.Item, 0, groups);

                // AutoCAD 2021 (F2, 20 LEADERs of Arial words exploded): the hook runs on to the far end of the words, "AV" 1200
                // units wide at 2.5 over 700 to the unit, held at (22, 10).
                DxfPolyline line = Assert.Single(leader.Explode().OfType<DxfPolyline>());
                Is(22 + (1200 * 2.5 / 700), line.GetPoint2dAt(line.NumberOfVertices - 1).X);
            });
        }

        // ---- which fonts are measured -------------------------------------------------------------------------------------------

        [Fact]
        public void AFaceAutoCadWouldNotDraw_IsNotMeasured_AndSaidOnceAtDebug()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    Style(db, "FACE", "gh.ttf", "GH Test");
                    Style(db, "BOLDFILE", "gh-bold.ttf", "GH Test", bold: true);
                    Style(db, "OTHER", "gh.ttf", "Times New Roman");
                    Style(db, "BOLD", "gh.ttf", "GH Test", bold: true);
                    Style(db, "ITALIC", "gh.ttf", "GH Test", italic: true);
                    Style(db, "TYPEFACE", string.Empty, "GH Test");
                    Style(db, "FIRST", "gh.ttc", "GH Test");
                    Style(db, "SECOND", "gh.ttc", "GH Other");
                    Style(db, "NOFACE", "gh.ttc");
                    Style(db, "CFF", "gh-cff.otf");
                    Style(db, "SYMBOLS", "gh-symbols.ttf");
                    Style(db, "BAD", "gh-bad.ttf");
                    GeoPoint3 unmeasured = Unmeasured("H", 1, 0, P(100, 50));
                    List<string> logged = null;

                    logged = Logged(() =>
                    {
                        // AutoCAD 2021 (F2) draws a style by its typeface where it has one: arial.ttf named Arial bold in arialbd.ttf,
                        // and Times New Roman in times.ttf; a collection named by no typeface it does not load.
                        foreach (string measured in new[] { "FACE", "BOLDFILE", "FIRST" })
                        {
                            Is(P(96.5, 50), Text(db, "H", 1, 0, P(100, 50), style: measured).Position);
                        }

                        foreach (string refused in new[] { "OTHER", "BOLD", "ITALIC", "TYPEFACE", "SECOND", "NOFACE", "CFF", "SYMBOLS", "BAD", "OTHER", "BAD" })
                        {
                            Is(unmeasured, Text(db, "H", 1, 0, P(100, 50), style: refused).Position);
                        }
                    });

                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("Times New Roman"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("GH Test bold"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("GH Other"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("no typeface"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("of no name"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("gh-cff.otf"));
                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("gh-symbols.ttf"));
                    Assert.Single(logged, m => m.StartsWith("Warn", StringComparison.Ordinal) && m.Contains("gh-bad.ttf"));
                },
                F("gh-bold.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(), "GH Test", 1)),
                F("gh.ttc", TrueTypeFile.Collection(TrueTypeFile.Letters())),
                F("gh-cff.otf", TrueTypeFile.Cff()),
                F("gh-symbols.ttf", TrueTypeFile.Font(TrueTypeFile.Letters().Select(l => new TrueTypeFile.Letter(l.Number > 0 && l.Number < 0x100 ? 0xF000 | l.Number : l.Number, l.Advance, l.Box ?? new int[0])).ToList(), "GH Symbols", 0, true)),
                F("gh-bad.ttf", Encoding.ASCII.GetBytes("\0\u0001\0\0\0\u0009")));
        }

        [Fact]
        public void EveryTrueTypeFontCutShort_OrOfABytePutWrong_IsReadOrRefused_AndNeverThrows()
        {
            byte[] font = TrueTypeFile.Font(TrueTypeFile.Letters(true));
            byte[] collection = TrueTypeFile.Collection(TrueTypeFile.Letters());
            var random = new Random(20261103);

            foreach (byte[] whole in new[] { font, collection })
            {
                Assert.NotNull(DxfTrueTypeFont.Read(whole));

                for (int length = 0; length <= whole.Length; length++)
                {
                    Measured(whole.Take(length).ToArray());
                }

                for (int round = 0; round < 3000; round++)
                {
                    byte[] wrong = (byte[])whole.Clone();

                    for (int k = random.Next(1, 4); k > 0; k--)
                    {
                        wrong[random.Next(wrong.Length)] = (byte)random.Next(256);
                    }

                    Measured(wrong);
                }
            }

            void Measured(byte[] bytes)
            {
                DxfTrueTypeFont.IsCff(bytes);
                DxfTrueTypeFont read = DxfTrueTypeFont.Read(bytes);

                if (read == null)
                {
                    return;
                }

                Assert.True(read.Above > 0.0);
                read.IsFace("GH Test", false, false);

                foreach (int number in new[] { ' ', 'A', 'V', 'H', 'g', 0xD8, 0xB0, 0x2205, 0x4E2D, 0x1F600, 0xFFFF })
                {
                    DxfFont.Glyph glyph = read.GlyphOf(number);
                    Assert.True(glyph == null || (glyph.Xs.Length == 2 && glyph.Ys[0] <= 0.0 && glyph.Ys[1] >= 0.0));
                }
            }
        }
    }
}
