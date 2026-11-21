using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the file tests share: a folder of their own under the temporary folder with the files AutoCAD's probes read
    /// (made as <c>tools/fixtures.py</c> of the L3 probes makes them), an interpreter whose current folder is that folder
    /// and whose <c>D</c> is its path with forward slashes, and the folder removed at the end.
    /// </summary>
    public abstract class LispFolderTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-files-" + Guid.NewGuid().ToString("N")
        );

        protected LispFolderTests()
        {
            Directory.CreateDirectory(_folder);
            Make("ansi.txt", "63 61 66 e9 0d 0a 6c 69 6e 65 32 0d 0a");
            Make("utf8.txt", "63 61 66 c3 a9 0d 0a c4 91 c3 a1 0d 0a");
            Make("utf8bom.txt", "ef bb bf 63 61 66 c3 a9 0d 0a c4 91 c3 a1 0d 0a");
            Make("utf16.txt", "ff fe 63 00 61 00 66 00 e9 00 0d 00 0a 00 11 01 e1 00 0d 00 0a 00");
            Make("lf.txt", "61 0a 62");
            Make("empty.txt", "");
            Make("long.txt", string.Join(" ", Enumerable.Repeat("78", 1000)) + " 0d 0a");
            Make("tabs.txt", "61 09 62 0d 0a 0d 0a 63 0d 0a");
            Make("cr.txt", "61 0d 62 0d 0a");
            Make("mixed.txt", "63 61 66 c3 a9 0d 0a 63 61 66 e9 0d 0a");
            Make("escape.txt", "5c 55 2b 30 31 31 31 78 0d 0a");
            Make("a-ansi.txt", "63 61 66 e9 0d 0a e9 0d 0a");
            Make("a-utf8.txt", "c3 a9 0d 0a");
            Make("a-ascii.txt", "61 62 63 0d 0a");
            Directory.CreateDirectory(Path.Combine(_folder, "dir"));
            Make("dir/a.lsp", "61 62 63");
            Make("dir/b.txt", "62");
            Make("dir/C.TXT", "");
            Directory.CreateDirectory(Path.Combine(_folder, "dir", "sub1"));
            Directory.CreateDirectory(Path.Combine(_folder, "dir", "sub2"));
        }

        /// <summary>Gets the folder, with backslashes, no separator at the end.</summary>
        protected string Folder => _folder;

        /// <summary>Gets the folder with forward slashes and a slash at the end: what <c>D</c> holds.</summary>
        protected string D => _folder.Replace('\\', '/') + "/";

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        /// <summary>Writes a file of the folder from bytes written in hexadecimal.</summary>
        protected void Make(string name, string hex) =>
            File.WriteAllBytes(
                Path.Combine(_folder, name),
                hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => byte.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                    .ToArray()
            );

        /// <summary>Reads the bytes of a file of the folder as hexadecimal, two digits each, separated by spaces.</summary>
        protected string Bytes(string name) =>
            string.Join(
                " ",
                File.ReadAllBytes(Path.Combine(_folder, name))
                    .Select(b => b.ToString("x2", CultureInfo.InvariantCulture))
            );

        /// <summary>Makes an interpreter that works in the folder and has <c>D</c> set.</summary>
        protected LispInterpreter NewLisp()
        {
            var lisp = new LispInterpreter { CurrentFolder = () => _folder };
            lisp.Run("(setq D \"" + D + "\")");
            return lisp;
        }

        protected static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        protected static void ExpectError(LispInterpreter lisp, string expression, string message)
        {
            LispResult result = lisp.Run(expression);
            Assert.True(result.Failed, expression);
            Assert.Equal(message, result.Error);
        }
    }

    /// <summary>
    /// <c>open</c>, <c>close</c>, <c>read-line</c>, <c>read-char</c>, <c>write-line</c>, <c>write-char</c> and the file
    /// argument of <c>princ</c>, <c>prin1</c> and <c>print</c>, against AutoCAD 2021's accoreconsole (probes r3, r5, r7 and r8
    /// of the L3 survey): the encodings read and written with the bytes checked, line ends, the file value and every error.
    /// The ANSI rows are those of a Windows code page 1252 machine.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispFileTests : LispFolderTests
    {
        private string Opened(string name) => "#<file \"" + D + name + "\">";

        [Fact]
        public void AnOpenedFile_IsAFileValueThatPrintsItsNameAsGiven()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (setq f (open (strcat D "ansi.txt") "r")) #<file "D:/.../ansi.txt">; (type f) FILE (r3)
            Expect(lisp, "(setq f (open (strcat D \"ansi.txt\") \"r\"))", Opened("ansi.txt"));
            Expect(lisp, "(type f)", "FILE");
            Expect(lisp, "(princ f)", Opened("ansi.txt") + Opened("ansi.txt"));
        }

        [Fact]
        public void AClosedFile_PrintsAsInternal_AndStaysAFile()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\"))");

            // AutoCAD 2021: (close f) nil; !f #<FILE internal>; (type f) FILE (r7)
            Expect(lisp, "(close f)", "nil");
            Expect(lisp, "f", "#<FILE internal>");
            Expect(lisp, "(type f)", "FILE");
            // AutoCAD 2021: closing again gives nil (r3)
            Expect(lisp, "(close f)", "nil");
        }

        [Fact]
        public void TwoOpensOfOneFile_AreNotEq_ButAFileIsEqualToItself()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\") g (open (strcat D \"ansi.txt\") \"r\"))");

            // AutoCAD 2021: (eq f g) nil; (equal f f) T (r3)
            Expect(lisp, "(eq f g)", "nil");
            Expect(lisp, "(equal f f)", "T");
            Expect(lisp, "(eq f f)", "T");
        }

        [Fact]
        public void AnAnsiFile_ReadsLineByLine_AndGivesNilAtTheEndAgainAndAgain()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\"))");

            // AutoCAD 2021: "caf\u00e9" "line2" nil nil (r3)
            Expect(lisp, "(read-line f)", "\"caf\u00e9\"");
            Expect(lisp, "(read-line f)", "\"line2\"");
            Expect(lisp, "(read-line f)", "nil");
            Expect(lisp, "(read-line f)", "nil");
        }

        [Theory]
        [InlineData("(setq f (open (strcat D \"ansi.txt\") \"r\"))", "\"caf\u00e9\"", "\"line2\"")] // AutoCAD 2021: (99 97 102 233) (r3)
        [InlineData("(setq f (open (strcat D \"utf8.txt\") \"r\"))", "\"caf\u00e9\"", "\"\u0111\u00e1\"")] // AutoCAD 2021: (99 97 102 233) then (273 225) (r3)
        [InlineData("(setq f (open (strcat D \"utf8bom.txt\") \"r\"))", "\"caf\u00e9\"", "\"\u0111\u00e1\"")] // AutoCAD 2021: a UTF-8 BOM is skipped (r3)
        [InlineData("(setq f (open (strcat D \"utf16.txt\") \"r\"))", "\"caf\u00e9\"", "\"\u0111\u00e1\"")] // AutoCAD 2021: a UTF-16 LE BOM decides (r3: (99 97 102 233))
        [InlineData("(setq f (open (strcat D \"utf8.txt\") \"r\" \"utf8\"))", "\"caf\u00e9\"", "\"\u0111\u00e1\"")] // AutoCAD 2021: "utf8" (r3)
        [InlineData(
            "(setq f (open (strcat D \"utf8bom.txt\") \"r\" \"utf8-bom\"))",
            "\"caf\u00e9\"",
            "\"\u0111\u00e1\""
        )] // AutoCAD 2021: "utf8-bom" opens (r8)
        [InlineData("(setq f (open (strcat D \"ansi.txt\") \"r\" \"utf8\"))", "\"caf?\"", "\"line2\"")] // AutoCAD 2021: the invalid byte is ? (63) (r3: (99 97 102 63))
        [InlineData("(setq f (open (strcat D \"mixed.txt\") \"r\"))", "\"caf\u00c3\u00a9\"", "\"caf\u00e9\"")] // AutoCAD 2021: a file not wholly UTF-8 is all ANSI (r4: (99 97 102 195 169) (99 97 102 233))
        public void AFileIsReadByTheEncodingTheRulesChoose(string open, string first, string second)
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run(open);

            Expect(lisp, "(read-line f)", first);
            Expect(lisp, "(read-line f)", second);
        }

        [Fact]
        public void AUtf16BigEndianFile_IsReadByItsMark()
        {
            // ours: D109 (the big-end mark too; AutoCAD was measured on little-end only)
            Make("utf16be.txt", "fe ff 00 63 00 e9 00 0d 00 0a");
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"utf16be.txt\") \"r\"))");

            Expect(lisp, "(read-line f)", "\"c\u00e9\"");
        }

        [Fact]
        public void TheEscapeInAFile_IsSevenCharacters()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"escape.txt\") \"r\") s (read-line f))");

            // AutoCAD 2021: a file holding \U+0111x reads as 8 characters (r4: (8 (273 120)) from vl-string->list, strlen 8)
            Expect(lisp, "(strlen s)", "8");
            Expect(lisp, "(substr s 1 3)", "\"\\\\U+\"");
        }

        [Fact]
        public void LfCrAndCrLf_EachEndALine_AndTheLastLineNeedsNoEnd()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: ("a" "b" nil) from "a\nb" (r3)
            lisp.Run("(setq f (open (strcat D \"lf.txt\") \"r\"))");
            Expect(lisp, "(list (read-line f) (read-line f) (read-line f))", "(\"a\" \"b\" nil)");
            // AutoCAD 2021: ("a\tb" "" "c" nil) (r3)
            lisp.Run("(setq f (open (strcat D \"tabs.txt\") \"r\"))");
            Expect(
                lisp,
                "(list (read-line f) (read-line f) (read-line f) (read-line f))",
                "(\"a\\tb\" \"\" \"c\" nil)"
            );
            // AutoCAD 2021: a lone CR ends a line: ("a" "b") from "a\rb\r\n" (r3)
            lisp.Run("(setq f (open (strcat D \"cr.txt\") \"r\"))");
            Expect(lisp, "(list (read-line f) (read-line f))", "(\"a\" \"b\")");
        }

        [Fact]
        public void AnEmptyFile_GivesNilForALineAndForACharacter()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"empty.txt\") \"r\"))");

            // AutoCAD 2021: nil nil (r3)
            Expect(lisp, "(read-line f)", "nil");
            Expect(lisp, "(read-char f)", "nil");
        }

        [Fact]
        public void ALineOfAThousandCharacters_ReadsWhole()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"long.txt\") \"r\"))");

            // AutoCAD 2021: (strlen (read-line f)) 1000 (r3)
            Expect(lisp, "(strlen (read-line f))", "1000");
        }

        [Fact]
        public void ReadChar_GivesTheCode_AndTenForAnyEndOfALine()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (97 9 98 10 10 99 10 nil nil) (r3)
            lisp.Run("(setq f (open (strcat D \"tabs.txt\") \"r\"))");
            Expect(
                lisp,
                "(list (read-char f) (read-char f) (read-char f) (read-char f) (read-char f) (read-char f) (read-char f) (read-char f) (read-char f))",
                "(97 9 98 10 10 99 10 nil nil)"
            );
            // AutoCAD 2021: (99 97 102 233 10 273) from UTF-8 (r3)
            lisp.Run("(setq f (open (strcat D \"utf8.txt\") \"r\"))");
            Expect(
                lisp,
                "(list (read-char f) (read-char f) (read-char f) (read-char f) (read-char f) (read-char f))",
                "(99 97 102 233 10 273)"
            );
            // AutoCAD 2021: (99 97 102 233 10) from ANSI (r3)
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\"))");
            Expect(
                lisp,
                "(list (read-char f) (read-char f) (read-char f) (read-char f) (read-char f))",
                "(99 97 102 233 10)"
            );
            // ours: D110, a lone CR is 10 as well
            lisp.Run("(setq f (open (strcat D \"cr.txt\") \"r\"))");
            Expect(lisp, "(list (read-char f) (read-char f) (read-char f) (read-char f))", "(97 10 98 10)");
        }

        [Fact]
        public void ReadLineAndReadChar_ShareThePlaceInTheFile()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\"))");

            // ours: D110, one position for both
            Expect(lisp, "(read-char f)", "99");
            Expect(lisp, "(read-line f)", "\"af\u00e9\"");
            Expect(lisp, "(read-char f)", "108");
        }

        [Fact]
        public void WritingWithNoEncoding_IsAnsi_WithCrLf_AndTheValueGivenBack()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-default.txt\") \"w\"))");

            // AutoCAD 2021: write-line gives its string, write-char its code; \u00e9 is 0xE9, \u0111 is written \U+0111 (r3)
            Expect(lisp, "(write-line (strcat \"caf\" (chr 233)) f)", "\"caf\u00e9\"");
            Expect(lisp, "(write-line \"\u0111\" f)", "\"\u0111\"");
            Expect(lisp, "(write-line \"x\" f)", "\"x\"");
            Expect(lisp, "(write-char 65 f)", "65");
            Expect(lisp, "(write-char 10 f)", "10");
            Expect(lisp, "(princ \"p\" f)", "\"p\"");
            Expect(lisp, "(prin1 \"q\" f)", "\"q\"");
            Expect(lisp, "(print \"r\" f)", "\"r\"");
            Expect(lisp, "(princ 1.5 f)", "1.5");
            Expect(lisp, "(close f)", "nil");

            // AutoCAD 2021 (w-default.txt): print writes CR LF before and a space after; nothing went to the screen
            Assert.Equal(
                "63 61 66 e9 0d 0a 5c 55 2b 30 31 31 31 0d 0a 78 0d 0a 41 0d 0a 70 22 71 22 0d 0a 22 72 22 20 31 2e 35",
                Bytes("w-default.txt")
            );
        }

        [Fact]
        public void WritingToAFile_WritesNothingOnTheScreen()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-quiet.txt\") \"w\"))");

            // AutoCAD 2021: the echo is the value alone, no text before it (r3)
            Assert.Equal("\"x\"", lisp.Run("(princ \"x\" f)").Output);
            Assert.Equal("53", lisp.Run("(write-char 53 f)").Output);
            lisp.Run("(close f)");
        }

        [Fact]
        public void WritingUtf8_HasNoBom_AndUtf8Bom_HasOne()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: "utf8" w-utf8.txt 63 61 66 c3 a9 c4 91 0d 0a; "utf8-bom" the same after ef bb bf (r3)
            lisp.Run("(setq f (open (strcat D \"w-utf8.txt\") \"w\" \"utf8\"))");
            lisp.Run("(write-line \"caf\u00e9\u0111\" f)");
            lisp.Run("(close f)");
            Assert.Equal("63 61 66 c3 a9 c4 91 0d 0a", Bytes("w-utf8.txt"));

            lisp.Run("(setq f (open (strcat D \"w-utf8bom.txt\") \"w\" \"utf8-bom\"))");
            lisp.Run("(write-line \"caf\u00e9\u0111\" f)");
            lisp.Run("(close f)");
            Assert.Equal("ef bb bf 63 61 66 c3 a9 c4 91 0d 0a", Bytes("w-utf8bom.txt"));
        }

        [Fact]
        public void AnUpperCaseMode_Works_AndWMakesAnEmptyFile()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (open ... "W") opens, and the file is empty after (close f) (r3)
            lisp.Run("(setq f (open (strcat D \"w-upper.txt\") \"W\"))");
            lisp.Run("(close f)");
            Assert.Equal("", Bytes("w-upper.txt"));
        }

        [Fact]
        public void W_EmptiesAnExistingFile()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: after "w" of a longer text and "w" again of "s", the size is 3 (r7)
            lisp.Run("(setq f (open (strcat D \"w-trunc.txt\") \"w\"))");
            lisp.Run("(write-line \"long line\" f)");
            lisp.Run("(close f)");
            lisp.Run("(setq f (open (strcat D \"w-trunc.txt\") \"w\"))");
            lisp.Run("(write-line \"s\" f)");
            lisp.Run("(close f)");
            Expect(lisp, "(vl-file-size (strcat D \"w-trunc.txt\"))", "3");
            Assert.Equal("73 0d 0a", Bytes("w-trunc.txt"));
        }

        [Fact]
        public void TheEscapeAndTheEuroSign_AreWrittenAsTheCodePageHasThem()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-u.txt\") \"w\"))");

            // AutoCAD 2021 (w-u.txt): the seven characters stay seven; the euro sign is 0x80; U+0080 is \U+0080;
            // write-char 273 is \U+0111, write-char 8364 is 0x80 (r5)
            lisp.Run("(write-line \"\\U+0111\" f)");
            lisp.Run("(write-line \"\u20ac\" f)");
            lisp.Run("(write-line \"\u0080\" f)");
            lisp.Run("(write-char 273 f)");
            lisp.Run("(write-char 8364 f)");
            lisp.Run("(write-char 9 f)");
            lisp.Run("(close f)");

            Assert.Equal(
                "5c 55 2b 30 31 31 31 0d 0a 80 0d 0a 5c 55 2b 30 30 38 30 0d 0a 5c 55 2b 30 31 31 31 80 09",
                Bytes("w-u.txt")
            );
            // AutoCAD 2021: reading it back, the first line is 7 characters (r5)
            lisp.Run("(setq f (open (strcat D \"w-u.txt\") \"r\") s (read-line f))");
            Expect(lisp, "(strlen s)", "7");
        }

        [Fact]
        public void TheEscape_IsWrittenAsSevenCharactersInUtf8Too()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-u8.txt\") \"w\" \"utf8\"))");

            // AutoCAD 2021 (w-u8.txt): 5c 55 2b 30 31 31 31 0d 0a c4 91 (r5)
            lisp.Run("(write-line \"\\U+0111\" f)");
            lisp.Run("(write-char 273 f)");
            lisp.Run("(close f)");

            Assert.Equal("5c 55 2b 30 31 31 31 0d 0a c4 91", Bytes("w-u8.txt"));
        }

        [Fact]
        public void ACharacterAbovePlaneZero_IsWrittenAsTwoEscapes()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-pair.txt\") \"w\"))");

            // ours: D109, one \U+XXXX for each half of the pair (not measured)
            lisp.Run("(write-line \"\ud83d\ude00\" f)");
            lisp.Run("(close f)");

            Assert.Equal("5c 55 2b 44 38 33 44 5c 55 2b 44 45 30 30 0d 0a", Bytes("w-pair.txt"));
        }

        [Fact]
        public void Appending_ToANewOrEmptyFile_IsAnsi()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: "a" to a file that is not there: e9 0d 0a (w-new-a.txt, r3); to an empty file: e9 0d 0a (r8)
            lisp.Run("(setq f (open (strcat D \"w-new-a.txt\") \"a\"))");
            lisp.Run("(write-line (chr 233) f)");
            lisp.Run("(close f)");
            Assert.Equal("e9 0d 0a", Bytes("w-new-a.txt"));

            lisp.Run("(setq f (open (strcat D \"empty.txt\") \"a\"))");
            lisp.Run("(write-line (chr 233) f)");
            lisp.Run("(close f)");
            Assert.Equal("e9 0d 0a", Bytes("empty.txt"));
        }

        [Fact]
        public void Appending_ToAnAsciiFile_IsUtf8()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: abc CR LF then c3 a9 CR LF; read back (97 98 99) and (233) (r8)
            lisp.Run("(setq f (open (strcat D \"a-ascii.txt\") \"a\"))");
            lisp.Run("(write-line (chr 233) f)");
            lisp.Run("(close f)");
            Assert.Equal("61 62 63 0d 0a c3 a9 0d 0a", Bytes("a-ascii.txt"));
            lisp.Run("(setq f (open (strcat D \"a-ascii.txt\") \"r\"))");
            Expect(lisp, "(read-line f)", "\"abc\"");
            Expect(lisp, "(read-line f)", "\"\u00e9\"");
        }

        [Fact]
        public void Appending_ToAnAnsiFile_StaysAnsi()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: a file with a lone 0xE9 is not UTF-8, so e9 0d 0a is added (r8)
            lisp.Run("(setq f (open (strcat D \"a-ansi.txt\") \"a\"))");
            lisp.Run("(write-line (chr 233) f)");
            lisp.Run("(close f)");
            Assert.Equal("63 61 66 e9 0d 0a e9 0d 0a e9 0d 0a", Bytes("a-ansi.txt"));
        }

        [Fact]
        public void Appending_WithUtf8_ToAUtf8File_IsUtf8()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: c3 a9 0d 0a then c3 a9 0d 0a (r8, a-utf8.txt)
            lisp.Run("(setq f (open (strcat D \"a-utf8.txt\") \"a\" \"utf8\"))");
            lisp.Run("(write-line (chr 233) f)");
            lisp.Run("(close f)");
            Assert.Equal("c3 a9 0d 0a c3 a9 0d 0a", Bytes("a-utf8.txt"));
        }

        [Fact]
        public void Appending_ToAFileWithABom_AddsNoSecondBom_AndStaysUtf8()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: ef bb bf 63 61 66 c3 a9 c4 91 0d 0a 6d 6f 72 65 0d 0a (r3, w-utf8bom.txt)
            lisp.Run("(setq f (open (strcat D \"utf8bom.txt\") \"a\"))");
            lisp.Run("(write-line \"more\" f)");
            lisp.Run("(close f)");
            Assert.Equal("ef bb bf 63 61 66 c3 a9 0d 0a c4 91 c3 a1 0d 0a 6d 6f 72 65 0d 0a", Bytes("utf8bom.txt"));
            // ours: D109, "utf8-bom" on a file that has text writes no BOM either
            lisp.Run("(setq f (open (strcat D \"a-utf8.txt\") \"a\" \"utf8-bom\"))");
            lisp.Run("(write-line \"z\" f)");
            lisp.Run("(close f)");
            Assert.Equal("c3 a9 0d 0a 7a 0d 0a", Bytes("a-utf8.txt"));
        }

        [Fact]
        public void Appending_WithUtf8Bom_ToANewFile_WritesTheBom()
        {
            LispInterpreter lisp = NewLisp();

            // ours: D109
            lisp.Run("(setq f (open (strcat D \"w-new-bom.txt\") \"a\" \"utf8-bom\"))");
            lisp.Run("(write-line \"z\" f)");
            lisp.Run("(close f)");
            Assert.Equal("ef bb bf 7a 0d 0a", Bytes("w-new-bom.txt"));
        }

        [Fact]
        public void WhatIsWritten_CanBeReadBeforeTheFileIsClosed()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-open.txt\") \"w\"))");
            lisp.Run("(write-line \"seen\" f)");

            // ours: D110, each write reaches the disk at once
            lisp.Run("(setq g (open (strcat D \"w-open.txt\") \"r\"))");
            Expect(lisp, "(read-line g)", "\"seen\"");
            lisp.Run("(close f)");
        }

        [Fact]
        public void ARelativeName_IsTakenFromTheCurrentFolder_AndKeepsItsNameInThePrintedFile()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (open "ghrel.txt" "w") #<file "ghrel.txt">, the file made in the current folder (r3)
            Expect(lisp, "(setq f (open \"ghrel.txt\" \"w\"))", "#<file \"ghrel.txt\">");
            lisp.Run("(write-line \"rel\" f)");
            lisp.Run("(close f)");
            Assert.Equal("72 65 6c 0d 0a", Bytes("ghrel.txt"));
        }

        [Theory]
        [InlineData("(open (strcat D \"nosuch.txt\") \"r\")")] // AutoCAD 2021: nil (r3)
        [InlineData("(open (strcat D \"nofolder/x.txt\") \"w\")")] // AutoCAD 2021: nil (r3)
        [InlineData("(open (strcat D \"x.txt\") \"rw\")")] // AutoCAD 2021: nil (r3)
        [InlineData("(open (strcat D \"dir\") \"r\")")] // ours: D108, a folder cannot be opened
        [InlineData("(open (strcat D \"dir\") \"w\")")] // ours: D108
        [InlineData("(open \"\" \"r\")")] // ours: D108
        public void AFileThatCannotBeOpened_IsNil(string form)
        {
            LispInterpreter lisp = NewLisp();

            Expect(lisp, form, "nil");
            Assert.False(File.Exists(Path.Combine(Folder, "x.txt")));
        }

        [Theory]
        [InlineData("(open (strcat D \"x.txt\") \"x\")", "unknown open mode: \"x\"")] // AutoCAD 2021: ; error: unknown open mode: "x" (r3)
        [InlineData("(open (strcat D \"x.txt\") \"r\" \"latin1\")", "unknown open mode: \"latin1\"")] // AutoCAD 2021: (r3)
        [InlineData("(open (strcat D \"x.txt\") \"w\" \"utf16\")", "unknown open mode: \"utf16\"")] // AutoCAD 2021: (r3)
        [InlineData("(open 5 \"r\")", "bad argument type: stringp 5")] // AutoCAD 2021: (r3)
        [InlineData("(open (strcat D \"ansi.txt\") 5)", "bad argument type: stringp 5")] // AutoCAD 2021: (r3)
        [InlineData("(open (strcat D \"ansi.txt\"))", "too few arguments")] // AutoCAD 2021: (r3)
        [InlineData("(write-line 5)", "bad argument type: stringp 5")] // AutoCAD 2021: (r3)
        [InlineData("(write-char \"a\")", "bad argument type: fixnump: \"a\"")] // AutoCAD 2021: (r3)
        [InlineData("(read-line 5)", "bad argument type: FILE 5")] // AutoCAD 2021: (r3)
        [InlineData("(close nil)", "bad argument type: streamp nil")] // AutoCAD 2021: (r3)
        [InlineData("(close 5)", "bad argument type: streamp 5")] // AutoCAD 2021: (r3)
        [InlineData("(princ \"x\" nil)", "bad argument type: FILE nil")] // AutoCAD 2021: (r3, L1 D7 kept)
        public void TheErrorsOfTheFileFunctions_AreAutoCADs(string form, string message)
        {
            ExpectError(NewLisp(), form, message);
        }

        [Fact]
        public void AFileOpenedToRead_CannotBeWrittenTo()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"ansi.txt\") \"r\"))");

            // AutoCAD 2021: ; error: bad argument type: output-streamp: #<file "D:/.../ansi.txt"> (r3)
            ExpectError(lisp, "(write-line \"x\" f)", "bad argument type: output-streamp: " + Opened("ansi.txt"));
            ExpectError(lisp, "(princ \"x\" f)", "bad argument type: output-streamp: " + Opened("ansi.txt"));
            ExpectError(lisp, "(write-char 65 f)", "bad argument type: output-streamp: " + Opened("ansi.txt"));
        }

        [Fact]
        public void AFileOpenedToWrite_CannotBeRead()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-mirror.txt\") \"w\"))");

            // ours: D110, the mirror of the error above
            ExpectError(lisp, "(read-line f)", "bad argument type: input-streamp: " + Opened("w-mirror.txt"));
            ExpectError(lisp, "(read-char f)", "bad argument type: input-streamp: " + Opened("w-mirror.txt"));
            lisp.Run("(close f)");
        }

        [Fact]
        public void AClosedFile_RefusesEveryUse_InTheWordsAutoCADUses()
        {
            LispInterpreter lisp = NewLisp();
            lisp.Run("(setq f (open (strcat D \"w-closed.txt\") \"w\"))");
            lisp.Run("(close f)");

            // AutoCAD 2021: write-line, princ and read-char say "stream is closed"; read-line a type error (r3, r7)
            ExpectError(lisp, "(write-line \"x\" f)", "stream is closed: #<FILE internal>");
            ExpectError(lisp, "(princ \"x\" f)", "stream is closed: #<FILE internal>");
            ExpectError(lisp, "(read-char f)", "stream is closed: #<FILE internal>");
            ExpectError(lisp, "(read-line f)", "bad argument type: FILE #<FILE internal>");
        }

        [Fact]
        public void WriteLineAndWriteChar_WithNoFileOrNil_WriteOnTheScreen()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: screen, then the echo "screen"; B, then 66 (r3)
            Expect(lisp, "(write-line \"screen\")", "screen\n\"screen\"");
            Expect(lisp, "(write-line \"screen\" nil)", "screen\n\"screen\"");
            Expect(lisp, "(write-char 66)", "B66");
            Expect(lisp, "(write-char 66 nil)", "B66");
        }

        [Fact]
        public void ReadLine_WithNoFile_ReadsTheKeyboardAndKeepsWhatIsLeftOfTheLine()
        {
            var typed = new System.Collections.Generic.Queue<string>(new[] { "hello world", "ab", "(1 2)" });
            int asked = 0;
            LispInterpreter lisp = NewLisp();
            lisp.KeyboardLine = () =>
            {
                asked++;
                return typed.Count > 0 ? typed.Dequeue() : null;
            };

            // AutoCAD 2021: hello world raw, spaces kept (r7)
            Expect(lisp, "(read-line)", "\"hello world\"");
            // AutoCAD 2021: (read-char) with ab gives 97, the next 98, then (read-line) gives "" with no new line asked (r7)
            Expect(lisp, "(read-char)", "97");
            Assert.Equal(2, asked);
            Expect(lisp, "(read-char)", "98");
            Expect(lisp, "(read-line)", "\"\"");
            Assert.Equal(2, asked);
            // AutoCAD 2021: (setq x (read-line)) then (1 2) gives "(1 2)", taken as text (r7)
            Expect(lisp, "(setq x (read-line))", "\"(1 2)\"");
            Expect(lisp, "x", "\"(1 2)\"");
            // ours: D111, nothing more typed gives nil
            Expect(lisp, "(read-line)", "nil");
            Expect(lisp, "(read-char)", "nil");
        }

        [Fact]
        public void ReadLine_WithNoKeyboard_IsNil()
        {
            // ours: D111, a bare interpreter has no keyboard
            var lisp = new LispInterpreter();

            Expect(lisp, "(read-line)", "nil");
            Expect(lisp, "(read-char)", "nil");
        }

        [Fact]
        public void LoadAndOpen_ReadTheSameBytesTheSameWay()
        {
            // L1's load keeps its behaviour on the shared encoding helper: UTF-8 when the bytes are, else ANSI
            Make("t.lsp", "28 73 65 74 71 20 78 20 22 63 61 66 e9 22 29");
            LispInterpreter lisp = NewLisp();

            Expect(lisp, "(load (strcat D \"t.lsp\"))", "\"caf\u00e9\"");
        }

        [Fact]
        public void TheEncodingHelper_ChoosesAsTheRulesSay()
        {
            Assert.Equal(65001, LispTextEncoding.Detect(new byte[] { 0xEF, 0xBB, 0xBF, 0x61 }, out int bom).CodePage);
            Assert.Equal(3, bom);
            Assert.Equal(1200, LispTextEncoding.Detect(new byte[] { 0xFF, 0xFE, 0x61, 0 }, out bom).CodePage);
            Assert.Equal(2, bom);
            Assert.Equal(65001, LispTextEncoding.Detect(new byte[] { 0x61, 0xC3, 0xA9 }, out bom).CodePage);
            Assert.Equal(0, bom);
            Assert.Equal(65001, LispTextEncoding.Detect(new byte[0], out bom).CodePage);
            Assert.Equal(
                LispTextEncoding.Ansi.CodePage,
                LispTextEncoding.Detect(new byte[] { 0x61, 0xE9 }, out bom).CodePage
            );
            Assert.Equal("a?b?", LispTextEncoding.DecodeUtf8(new byte[] { 0x61, 0xE9, 0x62, 0xC3 }, 0));
            Assert.Equal("\u00e9?", LispTextEncoding.DecodeUtf8(new byte[] { 0xC3, 0xA9, 0xC0 }, 0));
            Assert.Equal("?\u0111", LispTextEncoding.DecodeUtf8(new byte[] { 0xE9, 0xC4, 0x91 }, 0));
        }
    }
}
