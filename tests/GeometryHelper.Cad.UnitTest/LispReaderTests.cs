using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The reader of the AutoLISP interpreter against AutoCAD 2021's <c>read</c> in accoreconsole (probes p1 and p2): every
    /// row is a <c>(read "...")</c> line and the text AutoCAD printed for it. The corners are the reader's own:
    /// <c>.5</c> is an error, <c>-.5</c> reads as <c>-</c>, <c>a.b</c> reads as <c>A</c>, and an integer past 32 bits is a real.
    /// </summary>
    public class LispReaderTests
    {
        private static string Read(string text) => LispInterpreter.Print(new LispInterpreter().Read(text), true);

        private static string ReadError(string text) =>
            Assert.Throws<LispException>(() => new LispInterpreter().Read(text)).Message;

        [Theory]
        [InlineData("(a b)", "(A B)")] // AutoCAD 2021: (A B)
        [InlineData("1.5", "1.5")] // AutoCAD 2021: 1.5
        [InlineData("", "nil")] // AutoCAD 2021: nil
        [InlineData("  abc def", "ABC")] // AutoCAD 2021: ABC
        [InlineData(@"""str""", @"""str""")] // AutoCAD 2021: "str"
        [InlineData("a;comment\n", "A")] // AutoCAD 2021: A
        [InlineData("'a", "(QUOTE A)")] // AutoCAD 2021: (QUOTE A)
        [InlineData("#xFF", "#XFF")] // AutoCAD 2021: #XFF
        [InlineData("1.", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("-.5", "-")] // AutoCAD 2021: -
        [InlineData("+5", "5")] // AutoCAD 2021: 5
        [InlineData("1/2", "1/2")] // AutoCAD 2021: 1/2
        [InlineData("1e5", "100000.0")] // AutoCAD 2021: 100000.0
        [InlineData("1E5", "100000.0")] // AutoCAD 2021: 100000.0
        [InlineData("10.0e-2", "0.1")] // AutoCAD 2021: 0.1
        [InlineData("a.b", "A")] // AutoCAD 2021: A
        [InlineData("(a.b c)", "(A C)")] // AutoCAD 2021: (read "(a.b c)") (A C): the dot and what follows it in a symbol are dropped
        [InlineData("|a b|", "|A")] // AutoCAD 2021: |A
        [InlineData("nil", "nil")] // AutoCAD 2021: nil
        [InlineData("T", "T")] // AutoCAD 2021: T
        [InlineData("a(b)", "A")] // AutoCAD 2021: A
        [InlineData("abc\"d\"", "ABC")] // AutoCAD 2021: ABC
        [InlineData("1abc", "1ABC")] // AutoCAD 2021: 1ABC
        [InlineData("-", "-")] // AutoCAD 2021: -
        [InlineData("1+", "1+")] // AutoCAD 2021: 1+
        [InlineData(";;\n5", "5")] // AutoCAD 2021: 5
        [InlineData("#|c|# 6", "#|C|#")] // AutoCAD 2021: #|C|# (no block comment)
        [InlineData("99999999999", "1.0e+11")] // AutoCAD 2021: 1.0e+11
        [InlineData("-2147483648", "-2.14748e+09")] // AutoCAD 2021: -2.14748e+09
        [InlineData("-2147483649", "-2.14748e+09")] // AutoCAD 2021: -2.14748e+09
        [InlineData("0x10", "0X10")] // AutoCAD 2021: 0X10
        [InlineData("00012", "12")] // AutoCAD 2021: 12
        [InlineData("1e400", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("2147483647", "2147483647")] // AutoCAD 2021: (progn 2147483647) 2147483647
        [InlineData("2147483648", "2.14748e+09")] // AutoCAD 2021: (progn 2147483648) 2.14748e+09
        [InlineData("-0.0", "0.0")] // AutoCAD 2021: (progn -0.0) 0.0
        [InlineData("'()", "(QUOTE nil)")] // the quote of nil; AutoCAD 2021: (eq nil '()) T
        [InlineData("(1 . 2)", "(1 . 2)")] // AutoCAD 2021: (progn '(1 . 2)) (1 . 2)
        [InlineData("(1 . (2 3))", "(1 2 3)")] // AutoCAD 2021: (setq x '(1 . (2 3))) (1 2 3)
        [InlineData("(1 (2 . 3) \"x\" 4.5 nil t)", "(1 (2 . 3) \"x\" 4.5 nil T)")] // AutoCAD 2021: (1 (2 . 3) "x" 4.5 nil T)
        [InlineData("c:x", "C:X")] // AutoCAD 2021: (read "c:x") C:X
        [InlineData("AbC", "ABC")] // AutoCAD 2021: (progn 'AbC) ABC
        public void ATextIsReadAsAutoCadReadsIt(string text, string printed)
        {
            Assert.Equal(printed, Read(text));
        }

        [Theory]
        [InlineData(".5", "misplaced dot on input")] // AutoCAD 2021: ; error: misplaced dot on input
        [InlineData("(1 .2)", "misplaced dot on input")] // AutoCAD 2021: ; error: misplaced dot on input
        [InlineData("( . 2)", "misplaced dot on input")] // AutoCAD 2021: ; error: misplaced dot on input
        [InlineData("(1 . 2 3)", "extra cdrs in dotted pair on input")] // AutoCAD 2021: ; error: extra cdrs in dotted pair on input
        [InlineData(")", "extra right paren on input")] // AutoCAD 2021: ; error: extra right paren on input
        [InlineData("\"abc", "malformed string on input")] // AutoCAD 2021: ; error: malformed string on input
        [InlineData("(1", "malformed list on input")] // AutoCAD 2021: ; error: malformed list on input
        public void AMalformedTextIsRefusedWithTheMessageOfAutoCad(string text, string message)
        {
            Assert.Equal(message, ReadError(text));
        }

        [Theory]
        [InlineData(@"(princ ""\101"")", "A\"A\"")] // AutoCAD 2021: A"A"
        [InlineData(@"(prin1 ""\"""")", "\"\\\"\"\"\\\"\"")] // AutoCAD 2021: "\"""\""
        [InlineData(@"(prin1 ""a\nb\rc\td\ee"")", "\"a\\nb\\rc\\td\\ee\"\"a\\nb\\rc\\td\\ee\"")] // the escapes of the reader come back as written
        public void TheEscapesOfAStringAreReadAndWrittenBack(string text, string output)
        {
            Assert.Equal(output, new LispInterpreter().Run(text).Output);
        }

        [Fact]
        public void AStringThatSpansTwoLines_KeepsTheLineBreak()
        {
            // AutoCAD 2021: (setq s "x<newline>y") "x\ny"
            var lisp = new LispInterpreter();

            Assert.Equal("\"x\\ny\"", lisp.Run("(setq s \"x\ny\")").Output);
        }

        [Theory]
        [InlineData(@"""q\w""", "qw")] // AutoCAD 2021 (loading a .lsp): an unknown escape drops the backslash: \w is w
        [InlineData(@"""\x41""", "x41")] // AutoCAD 2021: \x41 is x41
        [InlineData(@"""\.""", ".")] // AutoCAD 2021: \. is .
        [InlineData(@"""\ """, " ")] // AutoCAD 2021: a backslash and a space is a space
        [InlineData(@"""\8""", "8")] // AutoCAD 2021: \8 is 8
        [InlineData(@"""\9""", "9")] // AutoCAD 2021: \9 is 9
        [InlineData(@"""\M+10041""", "M+10041")] // AutoCAD 2021: \M is M
        [InlineData(@"""\E""", "E")] // AutoCAD 2021: only lower-case \e is an escape: \E is E
        [InlineData(@"""\N""", "N")] // AutoCAD 2021: \N is N
        [InlineData(@"""\T""", "T")] // AutoCAD 2021: \T is T
        [InlineData(@"""\R""", "R")] // AutoCAD 2021: \R is R
        [InlineData(@"""\e""", "\u001b")] // AutoCAD 2021: \e is the escape character
        [InlineData(@"""\n\t\r""", "\n\t\r")] // AutoCAD 2021: \n \t \r are the line feed, tab and carriage return
        [InlineData(@"""\1""", "\u0001")] // AutoCAD 2021: octal \1 is character 1
        [InlineData(@"""\18""", "\u00018")] // AutoCAD 2021: \18 is character 1 then 8
        [InlineData(@"""\101\102""", "AB")] // AutoCAD 2021: \101\102 is AB
        [InlineData(@"""\0101""", "\b1")] // AutoCAD 2021: \0101 is character 8 then 1 (three digits at most)
        [InlineData(@"""\777""", "ÿ")] // AutoCAD 2021: \777 is character 255
        [InlineData(@"""\\n""", "\\n")] // AutoCAD 2021: \\n is a backslash then n
        [InlineData(@"""a\""b""", "a\"b")] // AutoCAD 2021: \" is a quote
        [InlineData(@"""\0""", "")] // AutoCAD 2021: a NUL ends the string: "\0" is empty
        [InlineData(@"""\08""", "")] // AutoCAD 2021: "\08" is empty
        [InlineData(@"""\400""", "")] // AutoCAD 2021: "\400" is empty (256 is NUL in a byte)
        [InlineData(@"""ab\0cd""", "ab")] // AutoCAD 2021: the NUL ends the string where it is
        [InlineData(@"""\U""", "UU")] // AutoCAD 2021: a \U with no + and four hex digits becomes UU
        [InlineData(@"""\U+12""", "UU+12")] // AutoCAD 2021: \U+12 is UU+12
        [InlineData(@"""\Uab""", "UUab")] // AutoCAD 2021: \Uab is UUab
        [InlineData(@"""\U+0041""", @"\U+0041")] // AutoCAD 2021: \U+0041 stays as seven characters, (strlen "\U+0041") is 7
        [InlineData(@"""\u+0041""", @"\u+0041")] // AutoCAD 2021: the u may be lower case: seven characters
        [InlineData(@"""\U+00e1""", @"\U+00e1")] // AutoCAD 2021: the hex digits may be lower case: seven characters
        [InlineData(@"""a\U+1EC5b""", @"a\U+1EC5b")] // AutoCAD 2021: (strlen "a\U+1EC5b") is 9
        public void AStringEscapeIsReadAsAutoCadReadsItFromAFile(string text, string value)
        {
            Assert.Equal(value, new LispInterpreter().Read(text));
        }

        [Fact]
        public void AUnicodeEscape_StaysSevenCharactersInTheValue()
        {
            // AutoCAD 2021: (strlen "\U+0041") 7
            Assert.Equal("7", new LispInterpreter().Run(@"(strlen ""\U+0041"")").Output);
        }

        [Fact]
        public void ASymbolInAnyCase_IsOneSymbol()
        {
            // AutoCAD 2021: (eq 'a 'A) T
            var lisp = new LispInterpreter();

            Assert.Same(lisp.Read("abc"), lisp.Read("AbC"));
            Assert.Equal("T", lisp.Run("(eq 'a 'A)").Output);
        }

        [Fact]
        public void TwoInterpreters_HaveSymbolsOfTheirOwn()
        {
            var first = new LispInterpreter();
            var second = new LispInterpreter();
            first.Run("(setq shared 1)");

            Assert.Equal(1, first.GetValue("shared"));
            Assert.Null(second.GetValue("shared"));
        }

        [Fact]
        public void ASymbolReadFromNil_IsNull()
        {
            Assert.Null(new LispInterpreter().Read("nil"));
            Assert.Null(new LispInterpreter().Read("()"));
        }

        [Fact]
        public void AnIntegerThatFitsIn32Bits_IsAnInt_AndOneThatDoesNot_IsAReal()
        {
            var lisp = new LispInterpreter();

            Assert.Equal(2147483647, lisp.Read("2147483647"));
            Assert.Equal(-2147483647, lisp.Read("-2147483647"));
            Assert.Equal(2147483648.0, lisp.Read("2147483648"));
        }

        [Fact]
        public void ABlockCommentBetweenBarAndSemicolon_IsSkipped()
        {
            // Not measured: the reference of AutoLISP writes ;| ... |; as an inline comment, and so does its VS Code extension.
            var lisp = new LispInterpreter();

            Assert.Equal("3", lisp.Run("(+ 1 ;| two\nlines |; 2)").Output);
        }

        [Fact]
        public void ALineCommentEndsAtTheLineEnd()
        {
            var lisp = new LispInterpreter();

            Assert.Equal("3", lisp.Run("(+ 1 ; one\n 2) ; two").Output);
        }

        [Theory]
        [InlineData("(setq q", 1, false)] // AutoCAD 2021: the prompt after (setq q is (_>
        [InlineData("(setq w (list", 2, false)] // AutoCAD 2021: the prompt after (setq w (list is ((_>
        [InlineData("(setq s \"x", 1, true)] // AutoCAD 2021: the prompt inside the string is ("_>
        [InlineData("(setq w (list\n1 2\n)", 1, false)] // AutoCAD 2021: ((_> 1 2 then (_> )
        [InlineData("(princ \"(\")", 0, false)] // a parenthesis in a string is not counted
        [InlineData("(princ) ; (", 0, false)] // nor one in a comment
        [InlineData("(a))", -1, false)] // an extra ) is negative
        [InlineData("", 0, false)]
        public void OpenParens_CountsTheParenthesesStillOpen(string text, int open, bool inString)
        {
            Assert.Equal(open, LispInterpreter.OpenParens(text, out bool inside));
            Assert.Equal(inString, inside);
        }

        [Fact]
        public void AFormThatIsOnlyReadUpToItsFirstForm_LeavesTheRest()
        {
            // AutoCAD 2021: (read "  abc def") ABC
            Assert.Equal("(A)", Read("(a) (b)"));
        }
    }
}
