using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>SHX fonts read from synthetic files, the letters of a text drawn as their strokes, and the fonts found, replaced and substituted.</summary>
    public sealed class FontTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();
        private readonly ITestOutputHelper _output;

        public FontTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Dispose() => _folder.Dispose();

        private static void Point(double x, double y, double[] stroke, int at)
        {
            Assert.Equal(x, stroke[2 * at], 9);
            Assert.Equal(y, stroke[(2 * at) + 1], 9);
        }

        // ---- The font itself

        [Fact]
        public void AUnifontReadsItsHeightsAndDrawsAMoveWithThePenDownAsAStroke()
        {
            ShxFont font = ShxFont.Read(ShxFiles.Unifont());

            Assert.Equal(ShxFontKind.Unifont, font.Kind);
            Assert.Equal(10.0, font.Above);
            Assert.Equal(2.0, font.Below);
            ShxGlyph i = font.GlyphOf('I');
            Assert.Equal(4.0, i.Advance, 9);
            double[] stroke = Assert.Single(i.Strokes);
            Assert.Equal(4, stroke.Length);
            Point(1, 0, stroke, 0);
            Point(1, 10, stroke, 1);
            Assert.Empty(font.GlyphOf(' ').Strokes);
            Assert.Null(font.GlyphOf('Z'));
        }

        [Fact]
        public void AnOctantArcOfNoCountIsAWholeCircleAboutItsCentreIn32Chords()
        {
            ShxGlyph o = ShxFont.Read(ShxFiles.Unifont()).GlyphOf('O');

            double[] circle = Assert.Single(o.Strokes);
            Assert.Equal(33, circle.Length / 2);
            Point(3, 0, circle, 0);
            Point(3, 0, circle, 32);

            for (int k = 0; k < 33; k++)
            {
                Assert.Equal(3.0, Math.Sqrt(Math.Pow(circle[2 * k] - 3.0, 2) + Math.Pow(circle[(2 * k) + 1] - 3.0, 2)), 9);
            }

            Assert.Equal(9.0, o.Advance, 9);
        }

        [Fact]
        public void ABulgeOf127IsAHalfCircleCounterClockwiseBelowItsChord()
        {
            ShxGlyph d = ShxFont.Read(ShxFiles.Unifont()).GlyphOf('D');

            double[] half = Assert.Single(d.Strokes);
            Assert.Equal(17, half.Length / 2);
            Point(0, 0, half, 0);
            Point(3, -3, half, 8);
            Point(6, 0, half, 16);
            Assert.Equal(7.0, d.Advance, 9);
        }

        [Fact]
        public void AShapeWithinIsDrawnWhereThePenStandsAndAScaleDividedHalvesTheMovesAfter()
        {
            ShxGlyph s = ShxFont.Read(ShxFiles.Unifont()).GlyphOf('S');

            Assert.Equal(2, s.Strokes.Count);
            Point(1, 0, s.Strokes[0], 0);
            Point(1, 10, s.Strokes[0], 1);
            Point(4, 0, s.Strokes[1], 0);
            Point(4, 2, s.Strokes[1], 1);
            Assert.Equal(5.0, s.Advance, 9);
        }

        [Fact]
        public void MovesBySignedBytesAndAPlacePushedAndPoppedDrawOneStroke_TheVerticalCodePassedOver()
        {
            ShxGlyph e = ShxFont.Read(ShxFiles.Unifont()).GlyphOf('E');

            double[] stroke = Assert.Single(e.Strokes);
            Assert.Equal(3, stroke.Length / 2);
            Point(0, 0, stroke, 0);
            Point(0, 10, stroke, 1);
            Point(4, 10, stroke, 2);
            Assert.Equal(6.0, e.Advance, 9);
        }

        [Fact]
        public void AnArcOfFractionsOfOctantsRunsFromItsStartToItsEndAngle()
        {
            ShxGlyph f = ShxFont.Read(ShxFiles.Unifont()).GlyphOf('F');

            double[] quarter = Assert.Single(f.Strokes);
            Point(0, 0, quarter, 0);
            Point(-3, 3, quarter, (quarter.Length / 2) - 1);
        }

        [Fact]
        public void AFontOfShapesReadsItsShapeZeroAndDrawsAShapeWithinByItsByte()
        {
            ShxFont font = ShxFont.Read(ShxFiles.Shapes());

            Assert.Equal(ShxFontKind.Shapes, font.Kind);
            Assert.Equal(6.0, font.Above);
            Assert.Equal(2.0, font.Below);
            Assert.Equal(3, font.Count);
            Point(0, 6, Assert.Single(font.GlyphOf('B').Strokes), 1);
            Point(2, 6, Assert.Single(font.GlyphOf(127).Strokes), 1);
        }

        [Fact]
        public void ABigFontOfNoHeightAboveIsDrawnEightHighAndOpensItsPairsByItsRanges()
        {
            ShxFont font = ShxFont.Read(ShxFiles.Big());

            Assert.Equal(ShxFontKind.BigFont, font.Kind);
            Assert.Equal(8.0, font.Above);
            Assert.True(font.Opens(0xD6));
            Assert.False(font.Opens(0x41));
            double[] stroke = Assert.Single(font.GlyphOf(0xD6D0).Strokes);
            Point(2, 0, stroke, 0);
            Point(2, 8, stroke, 1);
            Assert.Equal(10.0, font.GlyphOf(0xD6D0).Advance, 9);
        }

        [Fact]
        public void BytesOfAnotherFormOrCutShortAreNoFont()
        {
            byte[] whole = ShxFiles.Unifont();

            Assert.Null(ShxFont.Read(null));
            Assert.Null(ShxFont.Read(new byte[] { 1, 2, 3 }));
            Assert.Null(ShxFont.Read(System.Text.Encoding.ASCII.GetBytes("AutoCAD-86 unifont 9.9\r\n\x1A\0\0\0\0\0\0")));
            Assert.Null(ShxFont.Read(whole.Take(30).ToArray()));
            Assert.Null(ShxFont.Read(ShxFiles.Shapes().Take(40).ToArray()));
            Assert.Null(ShxFont.Read(ShxFiles.Big().Take(40).ToArray()));

            // A unifont whose last letter is cut short keeps the letters before it.
            Assert.NotNull(ShxFont.Read(whole.Take(whole.Length - 2).ToArray()).GlyphOf('I'));
        }

        // ---- Text drawn in a font

        private CadDocument Drawing(string fontFile, Action<DxfDatabase> more, params DxfEntity[] entities)
        {
            var database = new DxfDatabase("AC1018");
            var style = new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = fontFile };
            database.TextStyleTable.Add(style);
            more?.Invoke(database);

            foreach (DxfEntity entity in entities)
            {
                database.ModelSpace.AppendEntity(entity);

                if (entity is DxfDBText text)
                {
                    text.TextStyleName = "GH_SHX";
                }
                else if (entity is DxfMText mtext)
                {
                    mtext.TextStyleName = "GH_SHX";
                }
            }

            return CadDocument.From(database);
        }

        private static DxfDBText Text(string words, double height, Action<DxfDBText> more = null)
        {
            var text = new DxfDBText { TextString = words, Position = new GeoPoint3(0, 0, 0), Height = height };
            more?.Invoke(text);
            return text;
        }

        private static DisplayText2 Shown(CadDocument document) => document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();

        private CadFonts Fonts(string replacement = null, IEnumerable<KeyValuePair<string, string>> substitutions = null, Func<string, bool> families = null)
            => new CadFonts(new[] { _folder.FullPath }, replacement, substitutions, families ?? (f => f == "Arial"));

        [Fact]
        public void ATextOfAStyleOfAnShxFontFoundIsDrawnAsTheStrokesOfItsLettersScaledToItsHeight()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("test.shx", null, Text("II", 5.0));
            document.Fonts = Fonts();

            DisplayText2 shown = Shown(document);

            // 5 high in a font 10 above: half a unit to the unit; the second I starts where the first moved the pen, 4 on.
            Assert.Equal("test.shx", shown.FontName);
            Assert.Equal(2, shown.Strokes.Count);
            Point(0.5, 0, shown.Strokes[0], 0);
            Point(0.5, 5, shown.Strokes[0], 1);
            Point(2.5, 0, shown.Strokes[1], 0);
            Assert.Equal(4.0, shown.EstimatedWidth, 9);
            Assert.Equal(0.0, shown.Bounds.MinX, 9);
            Assert.Equal(4.0, shown.Bounds.MaxX, 9);
            Assert.Equal(-1.0, shown.Bounds.MinY, 9);
            Assert.Equal(5.0, shown.Bounds.MaxY, 9);
            Assert.Equal(0.0, shown.DistanceTo(0.5, 2.0), 9);
        }

        [Fact]
        public void TheWidthFactorStretchesTheLettersAcrossAndTheSlantLeansThemFromTheBaseline()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("test.shx", null, Text("I", 5.0, t =>
            {
                t.WidthFactor = 2.0;
                t.Oblique = Math.PI / 4.0;
            }));
            document.Fonts = Fonts();

            DisplayText2 shown = Shown(document);

            // The line's foot at 1 x 0.5 x 2 = 1, its top 5 up leant 5 across.
            Point(1.0, 0, shown.Strokes[0], 0);
            Point(6.0, 5, shown.Strokes[0], 1);
            Assert.Equal(4.0, shown.EstimatedWidth, 9);
        }

        [Fact]
        public void ATextWrittenBackwardsIsTurnedOverAlongItsBaseline_AndOneTurnedRunsAtItsAngle()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument backward = Drawing("test.shx", null, Text("I", 5.0, t => t.IsMirroredInX = true));
            CadDocument turned = Drawing("test.shx", null, Text("I", 5.0, t => t.Rotation = Math.PI / 2.0));
            backward.Fonts = Fonts();
            turned.Fonts = Fonts();

            Point(-0.5, 0, Shown(backward).Strokes[0], 0);
            Point(-0.5, 5, Shown(backward).Strokes[0], 1);
            Point(0, 0.5, Shown(turned).Strokes[0], 0);
            Point(-5, 0.5, Shown(turned).Strokes[0], 1);
        }

        [Fact]
        public void ATextCentredOnItsPointIsCentredOnTheBoxOfItsInkAsAutoCadJustifiesIt()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("test.shx", null, Text("II", 5.0, t =>
            {
                t.HorizontalMode = DxfTextHorizontalMode.TextCenter;
                t.AlignmentPoint = new GeoPoint3(10, 0, 0);
            }));
            document.Fonts = Fonts();

            DisplayText2 shown = Shown(document);

            // Inked from 0.5 to 2.5, centred on 10: the lines at 9 and 11, the box 2 wide about 10.
            Point(9, 0, shown.Strokes[0], 0);
            Point(11, 0, shown.Strokes[1], 0);
            Assert.Equal(9.0, shown.Bounds.MinX, 9);
            Assert.Equal(11.0, shown.Bounds.MaxX, 9);
        }

        [Fact]
        public void ATextOnTheRightEndsWithItsInk_AndOneEndingInASpaceWithItsLastPen()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            Action<DxfDBText> right = t =>
            {
                t.HorizontalMode = DxfTextHorizontalMode.TextRight;
                t.AlignmentPoint = new GeoPoint3(10, 0, 0);
            };
            var lines = new DxfMText { Contents = @"II \PI", Location = new GeoPoint3(10, 0, 0), TextHeight = 5.0, Attachment = DxfAttachmentPoint.TopRight };
            CadDocument inked = Drawing("test.shx", null, Text("II", 5.0, right));
            CadDocument spaced = Drawing("test.shx", null, lines);
            inked.Fonts = Fonts();
            spaced.Fonts = Fonts();

            // Inked to 2.5: the last line on 10. A line with a space after it ends at its last pen, 4 + 2.5: its second line
            // at 10 - 6.5 + 2.5; the line under it, of no space after, ends with its ink on 10.
            Point(10, 0, Shown(inked).Strokes[1], 0);
            Assert.Equal(6.0, Shown(spaced).Strokes[1][0], 9);
            Assert.Equal(10.0, Shown(spaced).Strokes[2][0], 9);
        }

        [Fact]
        public void ALetterTheFontHasNotIsItsQuestionMarkAndTheDegreeSignOfAFontOfShapesIs127()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            _folder.Write("shapes.shx", ShxFiles.Shapes());
            CadDocument unknown = Drawing("test.shx", null, Text("ZI", 5.0));
            CadDocument degree = Drawing("shapes.shx", null, Text("A%%d", 6.0));
            unknown.Fonts = Fonts();
            degree.Fonts = Fonts();

            // Z drawn as ?, which moves the pen 4 and draws nothing: the I after it starts 2 on.
            Point(2.5, 0, Assert.Single(Shown(unknown).Strokes), 0);
            Assert.Equal(4.0, Shown(unknown).EstimatedWidth, 9);

            // The A and then shape 127's line across the top, 4 on.
            Assert.Equal(2, Shown(degree).Strokes.Count);
            Point(4, 6, Shown(degree).Strokes[1], 0);
            Point(6, 6, Shown(degree).Strokes[1], 1);
        }

        [Fact]
        public void ALetterOfTwoBytesIsDrawnFromTheBigFontBesideTheStylesFont_ScaledToItsOwnHeight()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            _folder.Write("gbcbig.shx", ShxFiles.Big());
            CadDocument document = Drawing("test.shx", d => d.TextStyleTable["GH_SHX"].BigFontFileName = "gbcbig.shx", Text("I中", 4.0));
            document.Fonts = Fonts();

            DisplayText2 shown = Shown(document);

            // 中 is D6D0 in GBK; 4 high in a big font 8 above: its line at 1.6 + 2 x 0.5, 4 high.
            Assert.Equal(2, shown.Strokes.Count);
            Point(2.6, 0, shown.Strokes[1], 0);
            Point(2.6, 4, shown.Strokes[1], 1);
            Assert.Equal(1.6 + 5.0, shown.EstimatedWidth, 9);
        }

        [Fact]
        public void AnMTextsFontCodeBeforeItsWordsIsHonoured()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var mtext = new DxfMText { Contents = @"{\Ftest|c0;I}", Location = new GeoPoint3(0, 0, 0), TextHeight = 5.0 };
            CadDocument document = Drawing("arial.ttf", d => d.TextStyleTable["GH_SHX"].Font = new DxfFontDescriptor("Arial", false, false, 0, 34), mtext);
            document.Fonts = Fonts();

            DisplayText2 shown = Shown(document);

            Assert.Equal("test.shx", shown.FontName);
            Assert.Single(shown.Strokes);
        }

        [Fact]
        public void TheCanvasDrawsTheStrokesOfAnShxTextAndNothingBetweenThem()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("test.shx", null, Text("II", 100.0, t => t.Position = new GeoPoint3(20, 20, 0)));
            document.Fonts = Fonts();
            var view = new CadView(200, 150);
            view.ZoomTo(new DisplayBox(0, 0, 200, 150), 0.0);

            using (var bitmap = new SkiaSharp.SKBitmap(200, 150, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul))
            {
                using (var canvas = new SkiaSharp.SKCanvas(bitmap))
                using (var renderer = new Wpf.SkiaCanvasRenderer())
                {
                    renderer.Render(canvas, document, document.ModelSpace, view, new Wpf.CanvasOptions());
                }

                // Ten units to the font's unit: the two lines up at x 30 and 70, from y 20 to 120 of the drawing, rows 130 to
                // 30 on the screen; the gap between them and the space above dark.
                Func<int, int, bool> lit = (x, y) => Enumerable.Range(x - 1, 3).Any(c => bitmap.GetPixel(c, y).Red > 60);
                Assert.True(lit(30, 80));
                Assert.True(lit(70, 80));
                Assert.False(lit(50, 80));
                Assert.False(lit(30, 10));
            }
        }

        // ---- Fonts found, replaced and substituted

        [Fact]
        public void AFontIsLookedForInTheDrawingsFolderFirstThenInTheFoldersInOrder_OfAnyCase()
        {
            using (var second = new FontFolder())
            using (var drawing = new FontFolder())
            {
                _folder.Write("one.shx", ShxFiles.Unifont());
                second.Write("one.shx", ShxFiles.Unifont());
                second.Write("two.shx", ShxFiles.Unifont());
                var fonts = new CadFonts(new[] { _folder.FullPath, second.FullPath }, null, null);

                Assert.Equal(Path.Combine(_folder.FullPath, "one.shx"), fonts.Locate("ONE.SHX", null), StringComparer.OrdinalIgnoreCase);
                Assert.Equal(Path.Combine(second.FullPath, "two.shx"), fonts.Locate("two", null), StringComparer.OrdinalIgnoreCase);
                drawing.Write("one.shx", ShxFiles.Unifont());
                Assert.Equal(Path.Combine(drawing.FullPath, "one.shx"), new CadFonts(fonts.Folders, null, null).Locate("one", drawing.FullPath), StringComparer.OrdinalIgnoreCase);
                Assert.Null(fonts.Locate("three.shx", drawing.FullPath));
            }
        }

        [Fact]
        public void AFontReadIsKeptForEveryDocumentUntilItsFileChanges()
        {
            string path = _folder.Write("kept.shx", ShxFiles.Unifont());

            ShxFont first = CadFonts.Load(path);
            Assert.Same(first, CadFonts.Load(path));

            File.WriteAllBytes(path, ShxFiles.Shapes());
            ShxFont changed = CadFonts.Load(path);
            Assert.NotSame(first, changed);
            Assert.Equal(ShxFontKind.Shapes, changed.Kind);
        }

        [Fact]
        public void AFontNotFoundIsDrawnInTheReplacementAndSaidOnce()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("romans.shx", null, Text("I", 5.0), Text("II", 5.0));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = Fonts("test.shx");

            DisplayText2[] shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();
            document.Regen();
            document.GetDisplay(document.ModelSpace);

            Assert.All(shown, t => Assert.Equal("test.shx", t.FontName));
            Assert.All(shown, t => Assert.NotNull(t.Strokes));
            // The drawing's Standard style names txt, missing too: AutoCAD lists the fonts of every style.
            Assert.Equal(new[] { "Missing SHX files: romans.shx drawn in test.shx; txt drawn in test.shx." }, said);
        }

        [Fact]
        public void AFontNotFoundWithNoReplacementFoundIsDrawnInTheDrawingFontAsBefore()
        {
            CadDocument document = Drawing("romans.shx", null, Text("I", 5.0));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = Fonts("nofont.shx");

            DisplayText2 shown = Shown(document);

            Assert.Equal("romans.shx", shown.FontName);
            Assert.Null(shown.Strokes);
            Assert.Equal(new[] { "Missing SHX files: romans.shx drawn in the drawing font; txt drawn in the drawing font." }, said);
        }

        [Fact]
        public void AFileFoundThatIsNoFontIsTakenAsNotFoundAndNothingThrows()
        {
            _folder.Write("romans.shx", new byte[] { 0x41, 0x75, 0x74, 0x6F, 1, 2, 3 });
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("romans.shx", null, Text("I", 5.0));
            document.Fonts = Fonts("test.shx");

            Assert.Equal("test.shx", Shown(document).FontName);
        }

        [Fact]
        public void ATypefaceTheCanvasHasIsDrawnByIt_OneItHasNotByTheReplacement()
        {
            CadDocument arial = Drawing("arial.ttf", d => d.TextStyleTable["GH_SHX"].Font = new DxfFontDescriptor("Arial", false, false, 0, 34), Text("I", 5.0));
            CadDocument missing = Drawing("nofont.ttf", d => d.TextStyleTable["GH_SHX"].Font = new DxfFontDescriptor("No Such Face", false, false, 0, 34), Text("I", 5.0));
            var said = new List<string>();
            arial.FontMissing += (s, e) => said.Add(e.Message);
            missing.FontMissing += (s, e) => said.Add(e.Message);
            arial.Fonts = Fonts("Arial");
            missing.Fonts = Fonts("Arial");

            Assert.Equal("Arial", Shown(arial).FontName);
            Assert.Null(Shown(arial).Strokes);
            Assert.Equal("Arial", Shown(missing).FontName);
            // The typeface is no SHX file: said on its own, after the list of the Standard style's txt.
            Assert.Equal(new[] { "Missing SHX files: txt drawn in Arial.", "Font No Such Face not found: using Arial." }, said);
        }

        [Fact]
        public void AFontInTheSubstitutionTableIsDrawnInTheFontItIsGivenFor()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("romans.shx", null, Text("I", 5.0));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = Fonts(null, new[] { new KeyValuePair<string, string>("ROMANS", "test.shx") });

            Assert.Equal("test.shx", Shown(document).FontName);
            Assert.Empty(said);
        }

        [Fact]
        public void FontsSetAsTheSameAreKeptAndDrawNothingAgain()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Drawing("test.shx", null, Text("I", 5.0));
            document.Fonts = Fonts();
            CadFonts kept = document.Fonts;
            DisplayList2 shown = document.GetDisplay(document.ModelSpace);

            document.Fonts = Fonts();

            Assert.Same(kept, document.Fonts);
            Assert.Same(shown, document.GetDisplay(document.ModelSpace));
        }

        [Fact]
        public void AFullPathInAStyleIsUsedAsItIs_AndABareNameTakesShx()
        {
            using (var elsewhere = new FontFolder())
            {
                string path = elsewhere.Write("mine.shx", ShxFiles.Unifont());
                _folder.Write("bare.shx", ShxFiles.Unifont());
                var fonts = new CadFonts(new[] { _folder.FullPath }, null, null);

                Assert.Equal(path, fonts.Locate(path, null), StringComparer.OrdinalIgnoreCase);
                Assert.Null(fonts.Locate("mine.shx", null));
                Assert.Equal(Path.Combine(_folder.FullPath, "bare.shx"), fonts.Locate("bare", null), StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void TheFontsStartWithNoFolder_NoneOfAutodesk()
        {
            Assert.Empty(CadFonts.DefaultFolders);
            Assert.DoesNotContain(CadFonts.DefaultFolders, f => f.IndexOf("Autodesk", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ---- The built-in font

        [Fact]
        public void TheBuiltInFontDrawsAbc123FromTheBaselineToTheCapitalsHeight()
        {
            ShxFont font = CadFonts.BuiltIn;
            double advance = 0.0;
            double top = double.MinValue;
            double bottom = double.MaxValue;

            foreach (char c in "ABC123")
            {
                ShxGlyph glyph = font.GlyphOf(c);
                Assert.NotEmpty(glyph.Strokes);
                advance += glyph.Advance;

                foreach (double[] stroke in glyph.Strokes)
                {
                    for (int k = 1; k < stroke.Length; k += 2)
                    {
                        top = Math.Max(top, stroke[k]);
                        bottom = Math.Min(bottom, stroke[k]);
                    }
                }
            }

            // Hershey's Roman Simplex: capitals 21 above the baseline, descenders 7 below, as romans.shx; ABC123 is 18 + 21 + 21
            // + 20 + 20 + 20 = 120 units wide, 5.7 heights, inked from the baseline to the capitals' top.
            _output.WriteLine("ABC123: " + advance + " units, " + (advance / font.Above).ToString("0.000") + " heights wide, ink " + bottom + " to " + top);
            Assert.Equal(ShxFontKind.Unifont, font.Kind);
            Assert.Equal(98, font.Count);
            Assert.Equal(21.0, font.Above);
            Assert.Equal(7.0, font.Below);
            Assert.Equal(120.0, advance, 9);
            Assert.Equal(21.0, top, 9);
            Assert.Equal(0.0, bottom, 9);
            Assert.True(font.Has(0xB0));
            Assert.True(font.Has(0xB1));
            Assert.True(font.Has(0x2205));
            Assert.Equal(-7.0, font.GlyphOf('g').Strokes.SelectMany(s => s.Where((v, k) => k % 2 == 1)).Min(), 9);
        }

        [Fact]
        public void ATextInTheBuiltInFontHasItsCapitalsAsHighAsTheText()
        {
            CadDocument document = Drawing(CadFonts.BuiltInName, null, Text("ABC123", 2.5, t => t.Position = new GeoPoint3(10, 20, 0)));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);

            DisplayText2 shown = Shown(document);
            double[] ys = shown.Strokes.SelectMany(s => s.Where((v, k) => k % 2 == 1)).ToArray();
            double[] xs = shown.Strokes.SelectMany(s => s.Where((v, k) => k % 2 == 0)).ToArray();

            // 2.5 high in a font 21 above: the ink from the baseline at 20 to 22.5; 120 units across at 2.5 / 21 is 14.286, the
            // ink a little within it, from the foot of the A 1 unit in to the right of the 3 at 117.
            Assert.Equal(CadFonts.BuiltInName, shown.FontName);
            Assert.Equal(20.0, ys.Min(), 9);
            Assert.Equal(22.5, ys.Max(), 9);
            Assert.Equal(10.0 + (2.5 / 21.0), xs.Min(), 9);
            Assert.Equal(10.0 + (117.0 * 2.5 / 21.0), xs.Max(), 9);
            Assert.Empty(said);
        }

        [Fact]
        public void ARomansNotFoundInNoFolderIsDrawnInTheBuiltInFontAndSaidOnce()
        {
            CadDocument document = Drawing("romans.shx", null, Text("H", 5.0), Text("HH", 5.0));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);

            DisplayText2[] shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();
            document.Regen();
            document.GetDisplay(document.ModelSpace);

            Assert.All(shown, t => Assert.Equal(CadFonts.BuiltInName, t.FontName));
            Assert.All(shown, t => Assert.Equal(5.0, t.Strokes.SelectMany(s => s.Where((v, k) => k % 2 == 1)).Max(), 9));
            Assert.Equal(new[] { "Missing SHX files: romans.shx drawn in GeometryHelper Simplex; txt drawn in GeometryHelper Simplex." }, said);
        }

        // ---- AutoCAD's ways with SHX fonts: FONTALT, FONTMAP, big fonts and the list of missing files

        [Fact]
        public void ABigFontNotFoundIsDrawnInTheBigHalfOfTheReplacement_AndWithNoneAsQuestionMarks()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            _folder.Write("gbcbig.shx", ShxFiles.Big());
            Action<DxfDatabase> big = d => d.TextStyleTable["GH_SHX"].BigFontFileName = "nobig.shx";
            CadDocument halved = Drawing("test.shx", big, Text("I中", 4.0));
            CadDocument plain = Drawing("test.shx", big, Text("I中", 4.0));
            var said = new List<string>();
            halved.FontMissing += (s, e) => said.Add(e.Message);
            plain.FontMissing += (s, e) => said.Add(e.Message);
            halved.Fonts = Fonts("test.shx,gbcbig.shx");
            plain.Fonts = Fonts("test.shx");

            // 中 drawn from gbcbig.shx, its line up 4 at 1.6 + 1; with no big font, the question mark of test.shx draws nothing.
            Assert.Equal(2, Shown(halved).Strokes.Count);
            Point(2.6, 4, Shown(halved).Strokes[1], 1);
            Assert.Single(Shown(plain).Strokes);
            Assert.Equal(new[] { "Missing SHX files: nobig.shx (big font) drawn in gbcbig.shx; txt drawn in test.shx.", "Missing SHX files: nobig.shx (big font) drawn as question marks; txt drawn in test.shx." }, said);
        }

        [Fact]
        public void AFontMappingFileMapsAFontBeforeItIsLookedFor()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            string map = Path.Combine(_folder.FullPath, "mine.fmp");
            File.WriteAllText(map, "romans.shx;test.shx\r\n\r\nno semicolon\r\n;blank.shx\r\ntxt;\r\n");
            CadDocument document = Drawing("romans.shx", null, Text("I", 5.0));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);

            IReadOnlyList<KeyValuePair<string, string>> pairs = CadFonts.ReadFontMap(map);
            document.Fonts = Fonts(null, pairs);

            Assert.Equal(new[] { new KeyValuePair<string, string>("romans.shx", "test.shx") }, pairs);
            Assert.Equal("test.shx", Shown(document).FontName);
            Assert.Empty(said);
            Assert.Empty(CadFonts.ReadFontMap(Path.Combine(_folder.FullPath, "none.fmp")));
        }

        [Fact]
        public void TheMissingShxFilesAreListedOnceInTheOrderOfTheirNames_AndOneGivenAFontOfItsOwnLeavesTheList()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            // A second style, used by no text, names isocp.shx: AutoCAD lists the fonts of every style, drawn or not. An MTEXT's
            // \Fisocp after the list is the same font, not said again.
            var mtext = new DxfMText { Contents = @"{\Fisocp|c0;I}", Location = new GeoPoint3(0, 20, 0), TextHeight = 5.0 };
            CadDocument document = Drawing("romans.shx", d => d.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_ISO", FileName = "isocp.shx" }), Text("I", 5.0), mtext);
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);
            document.Fonts = Fonts(CadFonts.BuiltInName);

            document.GetDisplay(document.ModelSpace);
            document.Regen();
            document.GetDisplay(document.ModelSpace);

            Assert.Contains("isocp.shx drawn in GeometryHelper Simplex; romans.shx drawn in GeometryHelper Simplex", Assert.Single(said));
            Assert.Equal(new[] { "isocp.shx", "romans.shx" }, document.FindMissingFonts().Select(f => f.Name).Where(n => n != "txt"));

            document.Fonts = document.Fonts.WithSubstitution("romans.shx", "test.shx");

            Assert.DoesNotContain(document.FindMissingFonts(), f => f.Name == "romans.shx");
            Assert.Equal("test.shx", document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().First().FontName);
        }
    }
}
