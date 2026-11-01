using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The escapes <c>\U+XXXX</c> AutoCAD writes for the letters beyond the code page of a file of R2004 or older (D23e): an
    /// MTEXT's contents read with the letters they stand for, as AutoCAD 2021 reads them, a letter beyond U+FFFF from the
    /// escapes of its two halves; the letters written back as escapes in such a file and as they are in one of R2007 or later,
    /// as TEXT and ATTRIB already were.
    /// </summary>
    public class DxfTextEscapesD23eTests
    {
        // D with stroke, a Korean syllable, e acute (in Windows-1252) and a face beyond U+FFFF, U+1F600.
        private static readonly string Face = char.ConvertFromUtf32(0x1F600);
        private static readonly string Letters = "aĐb보" + Face + "cé";

        /// <summary>A drawing of a version with an MTEXT whose group 1 is written as given, as the file holds it.</summary>
        private static DxfDatabase Holding(string version, string written)
        {
            var db = new DxfDatabase(version);
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "GH_WORDS", Location = new GeoPoint3(0, 0, 0) });
            string text = db.ToText();
            Assert.Contains("\nGH_WORDS\n", text);
            return DxfDatabase.Parse(text.Replace("\nGH_WORDS\n", "\n" + written + "\n"));
        }

        [Fact]
        public void TheEscapesOfAnMTextOfR2004_AreReadAsTheLettersTheyStandFor_AFaceFromItsTwoHalves()
        {
            // As AutoCAD writes them in a DXF of R2004 of the code page ANSI_1252: the letters the page has not as escapes.
            DxfMText mtext = Holding("AC1018", "{\\H2x;a\\U+0110b\\U+BCF4\\U+D83D\\U+DE00cé}\\Pnext").ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal("{\\H2x;" + Letters + "}\\Pnext", mtext.Contents);
            Assert.Equal(Letters + "\nnext", mtext.Text);
        }

        [Fact]
        public void LettersSetOnAnMTextOfR2004_AreWrittenAsEscapes_AndReadBackAsTheLetters()
        {
            var db = new DxfDatabase("AC1018");
            var mtext = new DxfMText { Contents = Letters + "\\P{\\C1;" + Letters + "}" };
            db.ModelSpace.AppendEntity(mtext);
            string text = db.ToText();

            Assert.Contains("a\\U+0110b\\U+BCF4\\U+D83D\\U+DE00cé\\P{\\C1;a\\U+0110b", text);
            Assert.Equal(Letters + "\\P{\\C1;" + Letters + "}", mtext.Contents);
            Assert.Equal(Letters + "\\P{\\C1;" + Letters + "}", DxfDatabase.Parse(text).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Fact]
        public void AnMTextOfR2004GivenTheWordsItHas_AsTheyReadOrAsTheyAreWritten_ChangesNothing()
        {
            // Two pieces as another writer splits them, by the escaped words: split again by the letters they would differ.
            string first = string.Concat(Enumerable.Repeat("\\U+0110", 30)) + "x";
            DxfDatabase db = Holding("AC1018", "GH_SPLIT");
            string before = db.ToText().Replace("  1\nGH_SPLIT\n", "  3\n" + first + "\n  1\nend\n");
            Assert.Contains(first, before);
            db = DxfDatabase.Parse(before);
            DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal(new string('Đ', 30) + "xend", mtext.Contents);
            mtext.Contents = mtext.Contents;
            mtext.Contents = first + "end";
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void LettersSetOnAnMTextOfR2018_AreWrittenAsTheyAre_AFaceAsTheEscapesOfItsHalves_AndReadBackTheSame()
        {
            // D23f: AutoCAD 2021 opens no DXF of R2018 that holds the four bytes of UTF-8 of a letter beyond U+FFFF.
            var db = new DxfDatabase("AC1032");
            db.ModelSpace.AppendEntity(new DxfMText { Contents = Letters });
            string text = db.ToText();

            Assert.Contains("\naĐb보\\U+D83D\\U+DE00cé\n", text);
            Assert.DoesNotContain("\\U+0110", text);
            Assert.Equal(Letters, DxfDatabase.Parse(text).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1032")]
        public void AnEscapedBackslash_TheEscapeOfALetterOfAscii_AndHalfALetterAlone_AreReadAsAutoCad2021ReadsThem(string version)
        {
            // D23f, as AutoCAD 2021 reads them: \\U+0110 is a backslash and D with stroke, \U+0041 an A, \U+005C a backslash
            // that makes a code, \U+000A a line break and \U+D83D a half with no other half after it; too few digits are kept.
            const string Written = "x\\\\U+0110 \\U+0041 \\U+005CP \\U+000A \\U+D83Dy \\U+DE00 \\U+00e9 \\U+00G1 \\U+01";
            DxfMText mtext = Holding(version, Written).ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal("x\\Đ A \\P \n \uD83Dy \uDE00 é \\U+00G1 \\U+01", mtext.Contents);
        }

        [Fact]
        public void AFaceAtTheEndOfAPieceOf250Characters_IsWrittenWhole_InAFileOfR2018()
        {
            // The pieces of 250 characters (3) end before a letter whose halves the end would part, as before a caret.
            var db = new DxfDatabase("AC1032");
            string words = new string('x', 249) + Face + "end";
            db.ModelSpace.AppendEntity(new DxfMText { Contents = words });
            string text = db.ToText();

            // D23f: the face written as the escapes of its halves, as AutoCAD 2021 opens no DXF of R2018 holding its UTF-8.
            Assert.Contains("\n" + new string('x', 249) + "\n", text);
            Assert.Contains("\n\\U+D83D\\U+DE00end\n", text);
            Assert.Equal(words, DxfDatabase.Parse(text).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Fact]
        public void AFaceAtTheEndOfAPieceOf250Characters_ReadsWhole_InAFileOfR2004()
        {
            var db = new DxfDatabase("AC1018");
            string words = new string('x', 249) + Face + "end";
            db.ModelSpace.AppendEntity(new DxfMText { Contents = words });
            string text = db.ToText();

            Assert.Contains("\n" + new string('x', 249) + "\n", text);
            Assert.Contains("\n\\U+D83D\\U+DE00end\n", text);
            Assert.Equal(words, DxfDatabase.Parse(text).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Theory]
        [InlineData("AC1018", true)]
        [InlineData("AC1032", false)]
        public void TheLettersOfATextAndAnAttribute_AreWrittenAsEscapesBeforeR2007_AndReadBackAsTheLetters(string version, bool escaped)
        {
            var db = new DxfDatabase(version);
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = Letters });
            var block = new DxfBlockTableRecord { Name = "GH_TAGGED" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "v", "TAG", "p", null));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(0, 0, 0), Letters, "TAG", null));
            string text = db.ToText();
            DxfDatabase read = DxfDatabase.Parse(text);

            // D23f: in R2018 too the face is written as the escapes of its halves.
            Assert.Equal(escaped, text.Contains("a\\U+0110b\\U+BCF4\\U+D83D\\U+DE00cé"));
            Assert.Equal(!escaped, text.Contains("aĐb보\\U+D83D\\U+DE00cé"));
            Assert.Equal(Letters, read.ModelSpace.OfType<DxfDBText>().Single().TextString);
            Assert.Equal(Letters, read.ModelSpace.OfType<DxfBlockReference>().Single().AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
        }
    }
}
