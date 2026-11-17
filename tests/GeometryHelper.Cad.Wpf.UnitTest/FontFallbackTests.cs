using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A letter the font of a text has no glyph for is drawn from a font that has it, as AutoCAD 2021 draws it through the font
    /// linking of Windows: the Vietnamese letters of U+1EA0 to U+1EF9 are not in Malgun Gothic (a Korean font, as Gulim is),
    /// and were drawn as its empty box; they come out of Arial, as an MTEXT's <c>\fGulim|b0|i0|c129|p50;ỗ</c> of a drawing
    /// made on a Korean machine is read on a machine without Gulim's letters.
    /// </summary>
    public sealed class FontFallbackTests
    {
        private const string Vietnamese = "ỗ";

        private static SKTypeface Face(string family)
        {
            SKTypeface face = SKFontManager.Default.MatchFamily(family);
            Assert.NotNull(face);
            return face;
        }

        private static bool Has(SKTypeface face, string letter)
        {
            using (var font = new SKFont(face))
            {
                return font.ContainsGlyph(char.ConvertToUtf32(letter, 0));
            }
        }

        private static CadDocument Drawing(string family, string file, string words)
        {
            var database = new DxfDatabase("AC1027");
            var style = new DxfTextStyleTableRecord { Name = "OWN", FileName = file };
            style.Font = new DxfFontDescriptor(family, false, false, 0, 34);
            database.TextStyleTable.Add(style);
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = words,
                    Position = new GeoPoint3(0, 0, 0),
                    Height = 20.0,
                    TextStyleName = "OWN",
                }
            );
            return CadDocument.From(database, "words.dxf");
        }

        private static byte[] Picture(CadDocument document)
        {
            var view = new CadView(240, 120);
            view.ZoomTo(new DisplayBox(-5, -30, 55, 30));

            using (var bitmap = new SKBitmap(240, 120))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                canvas.Clear(SKColors.White);
                renderer.Render(
                    canvas,
                    document,
                    document.ModelSpace,
                    view,
                    new CanvasOptions
                    {
                        Paper = 0xFFFFFF,
                        Background = 0xFFFFFF,
                        VisibleLineColor = 0x000000,
                    }
                );
                return bitmap.Bytes;
            }
        }

        private static int Ink(byte[] picture) =>
            Enumerable.Range(0, picture.Length / 4).Count(i => picture[i * 4] < 128);

        /// <summary>The height and width in pixels of the ink of a picture 240 wide.</summary>
        private static int[] InkBox(byte[] picture)
        {
            int[] ink = Enumerable.Range(0, picture.Length / 4).Where(i => picture[i * 4] < 128).ToArray();
            return new[]
            {
                ink.Max(i => i % 240) - ink.Min(i => i % 240),
                ink.Max(i => i / 240) - ink.Min(i => i / 240),
            };
        }

        [Fact]
        public void TheFontsOfTheTest_MalgunGothicLacksTheVietnameseLetter_AndArialHasIt()
        {
            Assert.False(Has(Face("Malgun Gothic"), Vietnamese));
            Assert.True(Has(Face("Arial"), Vietnamese));
        }

        [Fact]
        public void ALetterTheFontLacks_IsDrawnFromAFontThatHasIt_NotAsTheBox()
        {
            byte[] korean = Picture(Drawing("Malgun Gothic", "malgun.ttf", Vietnamese));
            byte[] arial = Picture(Drawing("Arial", "arial.ttf", Vietnamese));

            Assert.True(Ink(arial) > 50, "The letter is drawn in Arial.");
            Assert.Equal(arial, korean);
        }

        [Fact]
        public void TheRunsOfALine_AreSplitAtTheLettersTheFontLacks_AndASciiOnlyLineIsOneRun()
        {
            SKTypeface malgun = Face("Malgun Gothic");

            List<KeyValuePair<string, SKTypeface>> runs = CadGlyphFallback.Runs(
                malgun,
                "ab" + Vietnamese + Vietnamese + "c"
            );

            Assert.Equal(new[] { "ab", Vietnamese + Vietnamese, "c" }, runs.Select(r => r.Key));
            Assert.Same(malgun, runs[0].Value);
            Assert.NotSame(malgun, runs[1].Value);
            Assert.True(Has(runs[1].Value, Vietnamese));
            Assert.Same(malgun, runs[2].Value);

            KeyValuePair<string, SKTypeface> plain = Assert.Single(CadGlyphFallback.Runs(malgun, "abc 123"));
            Assert.Same(malgun, plain.Value);
        }

        [Fact]
        public void ALetterNoFontHas_StaysInTheFontOfTheText_AndSoDoesASpace()
        {
            SKTypeface arial = Face("Arial");

            // U+10FFFF is no letter in any font: it stays with the text's font, which draws its box, and the space is its own.
            List<KeyValuePair<string, SKTypeface>> runs = CadGlyphFallback.Runs(arial, "a \U0010FFFF b");

            KeyValuePair<string, SKTypeface> whole = Assert.Single(runs);
            Assert.Same(arial, whole.Value);
            Assert.Equal("a \U0010FFFF b", whole.Key);
        }

        [Fact]
        public void TheLettersOfASymbolFont_StayInIt_ItsCharactersAreTheLettersOfAnotherFontsCodes()
        {
            SKTypeface wingdings = Face("Wingdings");

            KeyValuePair<string, SKTypeface> whole = Assert.Single(CadGlyphFallback.Runs(wingdings, "ü§Ø"));

            Assert.Same(wingdings, whole.Value);
        }

        [Fact]
        public void AnMTextRunInAFontThatLacksALetter_IsDrawnAsTheSameRunInArial_AsTheMTextOfAKoreanMachineIs()
        {
            CadDocument Mtext(string font)
            {
                var database = new DxfDatabase("AC1027");
                DxfTextStyleTableRecord style = database.TextStyleTable["Standard"];
                style.FileName = "arial.ttf";
                style.Font = new DxfFontDescriptor("Arial", false, false, 0, 34);
                database.ModelSpace.AppendEntity(
                    new DxfMText
                    {
                        Contents = @"{\f" + font + @"|b0|i0|c129|p50;" + Vietnamese + "}",
                        Location = new GeoPoint3(0, 0, 0),
                        TextHeight = 20.0,
                        TextStyleName = "Standard",
                    }
                );
                CadDocument document = CadDocument.From(database, "words.dxf");
                document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null, SkiaCanvasRenderer.HasFamily);
                return document;
            }

            byte[] korean = Picture(Mtext("Malgun Gothic"));
            byte[] arial = Picture(Mtext("Arial"));

            // The MTEXT sets its lines by the font of the run, so the two stand a pixel or two apart; the letter, with its
            // circumflex and tilde, is as high and as wide, which the font's empty box is not.
            Assert.True(Ink(arial) > 50, "The letter is drawn in Arial.");
            int[] wanted = InkBox(arial);
            int[] drawn = InkBox(korean);
            Assert.InRange(drawn[0], wanted[0] - 2, wanted[0] + 2);
            Assert.InRange(drawn[1], wanted[1] - 2, wanted[1] + 2);
            Assert.InRange(Ink(korean), Ink(arial) * 0.9, Ink(arial) * 1.1);
        }

        [Fact]
        public void KoreanLettersInAFamilyTheMachineHasNot_AreDrawnThroughTheFontLinking_NotAsQuestionMarks()
        {
            const string words = "한글 도면";
            Assert.Null(SKFontManager.Default.MatchFamily("DotumChe"));

            CadDocument Own(string family, string file)
            {
                CadDocument document = Drawing(family, file, words);
                document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null, SkiaCanvasRenderer.HasFamily);
                return document;
            }

            byte[] missing = Picture(Own("DotumChe", string.Empty));
            byte[] arial = Picture(Own("Arial", "arial.ttf"));

            // The drawing font is Arial, which has no Hangul: its letters come out of the font Windows links for them, as in a
            // text made in Arial. The single-stroke font would have drawn four question marks and a space.
            Assert.True(Ink(missing) > 200, "The Korean letters are drawn.");
            Assert.Equal(arial, missing);
        }

        [Fact]
        public void TheWidthOfALetterTheFontLacks_IsThatOfTheLetterDrawn()
        {
            double korean = SkiaCanvasRenderer.MeasureWidth("Malgun Gothic", false, false, Vietnamese);
            double arial = SkiaCanvasRenderer.MeasureWidth("Arial", false, false, Vietnamese);

            Assert.InRange(arial, 0.3, 1.0);
            Assert.Equal(arial, korean, 6);
        }
    }
}
