using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD 2021 showed of carets, multibyte codes, the RTF of an MTEXT and UTF-8 before R2007 (D23j, checked headless
    /// on files the library wrote): a caret and the character after it is the character 64 below it in every text, ^@ ends
    /// the text, a caret set is written ^ and a space; <c>\M+</c> of a letter alone and 00 is that letter, of a byte no letter
    /// of the page is kept as written; ContentsRTF keeps %% and what follows as written.
    /// </summary>
    public class DxfTextLeftoversD23jTests
    {
        private const string Placeholder = "GH_WORDS";

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        private static DxfDatabase Drawing(string version) => DxfDatabase.Parse(version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018);

        /// <summary>The drawing of the dimension tests of a version, its words written as given in an MTEXT, a TEXT, the ATTRIB of an INSERT and a MULTILEADER.</summary>
        private static DxfDatabase Holding(string version, string written)
        {
            DxfDatabase db = Drawing(version);
            db.ModelSpace.AppendEntity(new DxfMText { Contents = Placeholder, Location = P(0, 0), TextHeight = 2.5 });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = Placeholder, Position = P(0, 10), Height = 2.5 });
            var block = new DxfBlockTableRecord { Name = "GH_TAGGED" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfAttributeDefinition(P(0, 0), "v", "TAG", "p", null));
            var insert = new DxfBlockReference(P(0, 20), block);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(new DxfAttributeReference(P(0, 20), Placeholder, "TAG", null));
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = Placeholder, Location = P(30, 42), TextHeight = 2.5 } };
            mleader.AddLeaderLine(P(0, 30));
            db.ModelSpace.AppendEntity(mleader);
            string text = db.ToText();
            Assert.True(text.Split('\n').Count(line => line == Placeholder) >= 4);
            return DxfDatabase.Parse(text.Replace("\n" + Placeholder + "\n", "\n" + written + "\n"));
        }

        /// <summary>The words of each text of the drawing <see cref="Holding"/> makes: MTEXT, TEXT, ATTRIB and MULTILEADER.</summary>
        private static string[] Words(DxfDatabase db) => new[]
        {
            db.ModelSpace.OfType<DxfMText>().Single().Contents,
            db.ModelSpace.OfType<DxfDBText>().Single().TextString,
            db.ModelSpace.OfType<DxfBlockReference>().Single(b => b.Name == "GH_TAGGED").AttributeCollection.Cast<DxfAttributeReference>().Single().TextString,
            db.ModelSpace.OfType<DxfMLeader>().Single().MText.Contents,
        };

        // ---- carets ----------------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ACaretAndTheCharacterAfterIt_IsTheCharacter64BelowIt_InEveryText_AsAutoCadReadsIt(string version)
        {
            // AutoCAD 2021: ^J a line feed, ^I a tab, ^M a carriage return, ^ and a space a caret, ^a an exclamation mark, ^` a
            // space, ^^ U+001E, ^? U+FFFF.
            DxfDatabase db = Holding(version, "a^Jb^Ic^Md^ e^af^`g^^h^?i");

            Assert.All(Words(db), words => Assert.Equal("a\nb\tc\rd^e!f g\u001Eh￿i", words));
            Assert.Equal("a\nb\tcd^e!f g\u001Eh￿i", db.ModelSpace.OfType<DxfMText>().Single().Text);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ACaretAndAnAt_EndsTheText_InEveryText_AsAutoCadReadsIt(string version)
        {
            DxfDatabase db = Holding(version, "x^Iy^@z^Jw\\U+0000v");
            DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();

            Assert.All(Words(db), words => Assert.Equal("x\ty", words));
            Assert.Equal("x\ty", mtext.Text);
            Assert.EndsWith("}x\\tab y\\par}", mtext.ContentsRTF);
        }

        [Fact]
        public void ACaretSet_IsWrittenAsACaretAndASpace_AndReadsBackAsSet()
        {
            DxfDatabase db = Drawing("R2018");
            var mtext = new DxfMText { Contents = "m^2 a^Ib \\S1^2;", Location = P(0, 0), TextHeight = 2.5 };
            var text = new DxfDBText { TextString = "m^2 a^Ib\tc", Position = P(0, 10), Height = 2.5 };
            db.ModelSpace.AppendEntity(mtext);
            db.ModelSpace.AppendEntity(text);

            Assert.Equal("m^2 a^Ib \\S1^2;", mtext.Contents);
            Assert.Equal("m^2 a^Ib 1/2", mtext.Text);
            Assert.Equal("m^2 a^Ib\tc", text.TextString);

            string saved = db.ToText();
            Assert.Contains("\nm^ 2 a^ Ib \\S1^ 2;\n", saved);
            Assert.Contains("\nm^ 2 a^ Ib^Ic\n", saved);

            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal("m^2 a^Ib \\S1^2;", again.ModelSpace.OfType<DxfMText>().Single().Contents);
            Assert.Equal("m^2 a^Ib\tc", again.ModelSpace.OfType<DxfDBText>().Single().TextString);
        }

        // ---- ContentsRTF -----------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("x%%%y", "x%%%y")]
        [InlineData("x%%248y", "x%%248y")]
        [InlineData("x%%065y", "x%%065y")]
        [InlineData("x%%cy", "x%%cy")]
        [InlineData("x%%D%%Py", "x%%D%%Py")]
        [InlineData("x%%%%%%y", "x%%%%%%y")]
        [InlineData("x\\U+00F8y", "x\\u248?y")]
        public void TheRtfOfAnMText_KeepsItsSpecialCharactersAsWritten_AsAutoCadGivesIt(string contents, string body)
        {
            var mtext = new DxfMText { Contents = contents };

            Assert.Equal("{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0{\\*\\ghfontfile}txt;}}" + body + "\\par}", mtext.ContentsRTF);
        }

        [Fact]
        public void TheRtfOfAnMTextOfALineFeedReadFromACaret_IsALine()
        {
            DxfMText mtext = Holding("R2018", "x^Jy").ModelSpace.OfType<DxfMText>().Single();

            Assert.EndsWith("}x\\line y\\par}", mtext.ContentsRTF);
        }

        // ---- multibyte codes -------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AMultibyteCodeOfALetterAloneAndNought_IsThatLetter_InEveryText_AsAutoCadReadsIt(string version)
        {
            // Shift-JIS A4 and 41, GB 2312 80 (the euro sign there), Johab 82, Big5 80 and 20.
            const string Written = "x\\M+1A400y \\M+14100 \\M+58000 \\M+48200 \\M+28000 \\M+12000z";
            DxfDatabase db = Holding(version, Written);

            Assert.All(Words(db), words => Assert.Equal("x､y A € \u0082 \u0080  z", words));
            Assert.Equal("x､y A € \u0082 \u0080  z", db.ModelSpace.OfType<DxfMText>().Single().Text);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AMultibyteCodeOfAByteNoLetterOfItsPage_IsKeptAsWritten_InEveryText(string version)
        {
            // AutoCAD gives each a letter of its own from U+E800 on, in the order it meets them in all it reads; the bytes A0, FD,
            // FE and FF of Shift-JIS, 00 then FF, and a letter alone then one of these.
            const string Written = "x\\M+100FFy \\M+1A0A1 \\M+1FD41 \\M+1A1FF \\M+180FF \\M+1A4FF \\M+1A1A0 \\M+1FEFE \\M+200FF \\M+481FF \\M+580FF";
            DxfDatabase db = Holding(version, Written);

            Assert.All(Words(db), words => Assert.Equal(Written, words));
            Assert.Equal(Written, db.ModelSpace.OfType<DxfMText>().Single().Text);
        }

        // ---- UTF-8 before R2007 ----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void AFileBeforeR2007WhoseBytesAreUtf8_IsReadAsUtf8(string version)
        {
            // AutoCAD 2021 reads its letters in the code page of the header, café as cafÃ©, and refuses one with the mark of
            // UTF-8 at its head; the library takes such a file for UTF-8, as it has done before.
            var made = new DxfDatabase(version);
            made.ModelSpace.AppendEntity(new DxfDBText { TextString = Placeholder, Position = P(0, 0), Height = 2.5 });
            string text = made.ToText().Replace("\n" + Placeholder + "\n", "\ncafé Đường\n");
            string path = Path.Combine(Path.GetTempPath(), "gh-d23j-" + version + "-" + System.Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
                Assert.Equal("café Đường", DxfDatabase.Open(path).ModelSpace.OfType<DxfDBText>().Single().TextString);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
