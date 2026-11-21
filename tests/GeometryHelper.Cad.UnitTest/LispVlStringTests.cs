using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The <c>vl-string-</c> functions, <c>vl-string-&gt;list</c> and <c>vl-list-&gt;string</c>, and the Unicode rows around
    /// them (<c>chr</c> above 255, <c>strcase</c> of letters outside ASCII, <c>\U+XXXX</c> counted as seven characters),
    /// against AutoCAD 2021's accoreconsole: probes r2, r5, r6 and r7 of the L3 measurements. Each row is an expression and
    /// the text AutoCAD printed for it, an error as the message after <c>; error: </c>. Where AutoCAD's console showed a
    /// character it cannot draw as an empty string, the row asks for the codes or the length instead.
    /// </summary>
    [Collection("LispSerial")]
    public class LispVlStringTests
    {
        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        [Theory]
        [InlineData("(vl-string-search \"cd\" \"abcdcd\")", "2")] // AutoCAD 2021: 2 (r2)
        [InlineData("(vl-string-search \"cd\" \"abcdcd\" 3)", "4")] // AutoCAD 2021: 4 (r2)
        [InlineData("(vl-string-search \"x\" \"abc\")", "nil")] // AutoCAD 2021: nil (r2)
        [InlineData("(vl-string-search \"\" \"abc\")", "3")] // AutoCAD 2021: 3, the empty pattern is at the end (r2)
        [InlineData("(vl-string-search \"\" \"abc\" 2)", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-search \"C\" \"abc\")", "nil")] // AutoCAD 2021: nil, by case (r2)
        [InlineData("(vl-string-search \",\" \"1,2\")", "1")] // AutoCAD 2021: 1 (r2)
        [InlineData("(vl-string-search \"a\" \"abc\" 3)", "nil")] // AutoCAD 2021: nil, start at the length (r6)
        [InlineData("(vl-string-search \"a\" 'abc)", "nil")] // AutoCAD 2021: nil, the symbol is ABC (r5)
        [InlineData("(vl-string-search \"B\" 'abc)", "1")] // ours: D105, a symbol stands for its name, upper case
        public void VlStringSearch_FindsAPatternByCase(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-string-subst \"X\" \"b\" \"abcb\")", "\"aXcb\"")] // AutoCAD 2021: "aXcb" (r2)
        [InlineData("(vl-string-subst \"X\" \"b\" \"abcb\" 2)", "\"abcX\"")] // AutoCAD 2021: "abcX" (r2)
        [InlineData("(vl-string-subst \"X\" \"z\" \"abc\")", "\"abc\"")] // AutoCAD 2021: "abc" (r2)
        [InlineData("(vl-string-subst \"\" \"b\" \"abc\")", "\"ac\"")] // AutoCAD 2021: "ac" (r2)
        [InlineData("(vl-string-subst \"X\" \"\" \"abc\")", "\"abcX\"")] // AutoCAD 2021: "abcX" (r2)
        [InlineData("(vl-string-subst \"Y\" \"a\" \"aXa\")", "\"YXa\"")] // AutoCAD 2021: "YXa" (r2)
        public void VlStringSubst_ReplacesTheFirstOnly(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-string-position 98 \"abcb\")", "1")] // AutoCAD 2021: 1 (r2)
        [InlineData("(vl-string-position 98 \"abcb\" 2)", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-position 98 \"abcb\" nil T)", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-position 98 \"abcb\" 2 T)", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-position 120 \"abc\")", "nil")] // AutoCAD 2021: nil (r2)
        [InlineData("(vl-string-position 98 \"abcbb\" 0 T)", "4")] // AutoCAD 2021: 4 (r5)
        [InlineData("(vl-string-position 98 \"abcbb\" 1 T)", "4")] // AutoCAD 2021: 4 (r5)
        [InlineData("(vl-string-position 98 \"abcbb\" 3 T)", "4")] // AutoCAD 2021: 4 (r5)
        [InlineData("(vl-string-position 97 \"abca\" 2 T)", "3")] // AutoCAD 2021: 3 (r5)
        [InlineData("(vl-string-position 97 \"abcd\" 2 T)", "nil")] // AutoCAD 2021: nil, the last one is before start (r6)
        [InlineData("(vl-string-position 97 \"abca\" 3 T)", "3")] // AutoCAD 2021: 3 (r6)
        [InlineData("(vl-string-position 97 \"abca\" 4 T)", "nil")] // AutoCAD 2021: nil (r6)
        [InlineData("(vl-string-position 97 \"abca\" 4)", "nil")] // AutoCAD 2021: nil (r6)
        public void VlStringPosition_FindsACharacterCodeFromTheStartOrTheEnd(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-string-mismatch \"abcd\" \"abxd\")", "2")] // AutoCAD 2021: 2 (r2)
        [InlineData("(vl-string-mismatch \"abcd\" \"ABcd\")", "0")] // AutoCAD 2021: 0 (r2)
        [InlineData("(vl-string-mismatch \"abcd\" \"ABcd\" 0 0 T)", "4")] // AutoCAD 2021: 4 (r2)
        [InlineData("(vl-string-mismatch \"xabc\" \"abc\" 1 0)", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-mismatch \"abc\" \"abc\")", "3")] // AutoCAD 2021: 3 (r2)
        [InlineData("(vl-string-mismatch \"\" \"abc\")", "0")] // AutoCAD 2021: 0 (r2)
        [InlineData("(vl-string-elt \"abc\" 1)", "98")] // AutoCAD 2021: 98 (r2)
        [InlineData("(vl-string-elt \"\\U+0111x\" 0)", "92")] // AutoCAD 2021: 92, the backslash of the seven characters (r5)
        public void VlStringMismatchAndVlStringElt_CountAndReadCharacters(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-string-translate \"abc\" \"AB\" \"aabbcc\")", "\"AABBcc\"")] // AutoCAD 2021: "AABBcc" (r2)
        [InlineData("(vl-string-translate \"abc\" \"ABCD\" \"aabbcc\")", "\"AABBCC\"")] // AutoCAD 2021: "AABBCC" (r2)
        [InlineData("(vl-string-translate \"\" \"\" \"abc\")", "\"abc\"")] // AutoCAD 2021: "abc" (r2)
        [InlineData("(vl-string-trim \" \" \"  a b  \")", "\"a b\"")] // AutoCAD 2021: "a b" (r2)
        [InlineData("(vl-string-trim \" \\t\\n\" \"\\n\\t a \\t\")", "\"a\"")] // AutoCAD 2021: "a" (r2)
        [InlineData("(vl-string-left-trim \"ab\" \"abbacab\")", "\"cab\"")] // AutoCAD 2021: "cab" (r2)
        [InlineData("(vl-string-right-trim \"ab\" \"babcab\")", "\"babc\"")] // AutoCAD 2021: "babc" (r2)
        [InlineData("(vl-string-trim \"\" \" a \")", "\" a \"")] // AutoCAD 2021: " a " (r2)
        [InlineData("(vl-string-trim \"a\" \"aaa\")", "\"\"")] // AutoCAD 2021: "" (r2)
        [InlineData("(vl-string-left-trim \"\\\\\" \"\\\\a\")", "\"a\"")] // AutoCAD 2021: "a", the set is one backslash (r2)
        [InlineData("(vl-string-left-trim \"\" \"\")", "\"\"")] // AutoCAD 2021: "" (r5)
        [InlineData("(vl-string-trim 'a \"aba\")", "\"aba\"")] // AutoCAD 2021: "aba", the symbol is A (r5)
        public void VlStringTranslateAndTheTrims_WorkOnCharacterSets(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-string-search 1 \"abc\")", "bad argument type: (or stringp symbolp): 1")] // AutoCAD 2021: ; error: bad argument type: (or stringp symbolp): 1 (r2)
        [InlineData("(vl-string-search \"a\" 1)", "bad argument type: (or stringp symbolp): 1")] // AutoCAD 2021: ; error: bad argument type: (or stringp symbolp): 1 (r2)
        [InlineData("(vl-string-search \"c\" \"abc\" 9)", "bad argument value: string position out of range 9")] // AutoCAD 2021: ; error: bad argument value: string position out of range 9 (r2)
        [InlineData("(vl-string-search \"a\" \"abc\" -1)", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1 (r2)
        [InlineData("(vl-string-position \"b\" \"abc\")", "bad argument type: fixnump: \"b\"")] // AutoCAD 2021: ; error: bad argument type: fixnump: "b" (r2)
        [InlineData("(vl-string-position 97 \"abca\" 9)", "bad argument value: string position out of range 9")] // AutoCAD 2021: ; error: bad argument value: string position out of range 9 (r5)
        [InlineData("(vl-string-position 97 \"abca\" -1)", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1 (r5)
        [InlineData("(vl-string-position 98 \"abcbb\" 9 T)", "bad argument value: string position out of range 9")] // AutoCAD 2021: ; error: bad argument value: string position out of range 9 (r5)
        [InlineData("(vl-string-elt \"abc\" 3)", "bad argument value: string position out of range 3")] // AutoCAD 2021: ; error: bad argument value: string position out of range 3 (r2)
        [InlineData("(vl-string-elt \"abc\" -1)", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1 (r2)
        [InlineData("(vl-string-elt \"abc\" 1.0)", "bad argument type: fixnump: 1.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.0 (r5)
        [InlineData("(vl-string-trim 1 \"a\")", "bad argument type: (or stringp symbolp): 1")] // AutoCAD 2021: ; error: bad argument type: (or stringp symbolp): 1 (r2)
        [InlineData("(vl-string-mismatch \"abc\" \"abd\" 5 0)", "bad argument value: string position out of range 5")] // AutoCAD 2021: ; error: bad argument value: string position out of range 5 (r5)
        [InlineData("(vl-string-mismatch \"abc\" \"abd\" 0 -1)", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1 (r5)
        [InlineData("(vl-string-subst 5 \"a\" \"abc\")", "bad argument type: (or stringp symbolp): 5")] // AutoCAD 2021: ; error: bad argument type: (or stringp symbolp): 5 (r5)
        [InlineData("(vl-string-translate 5 \"a\" \"abc\")", "bad argument type: (or stringp symbolp): 5")] // AutoCAD 2021: ; error: bad argument type: (or stringp symbolp): 5 (r5)
        [InlineData("(vl-string->list 5)", "bad argument type: stringp 5")] // AutoCAD 2021: ; error: bad argument type: stringp 5 (r2)
        [InlineData("(vl-list->string '(97 1.5))", "bad char code in list: 1.5")] // AutoCAD 2021: ; error: bad char code in list: 1.5 (r2)
        [InlineData("(vl-list->string '(97 \"b\"))", "bad char code in list: \"b\"")] // AutoCAD 2021: ; error: bad char code in list: "b" (r2)
        [InlineData("(vl-list->string \"a\")", "bad argument type: listp \"a\"")] // AutoCAD 2021: ; error: bad argument type: listp "a" (r2)
        public void TheVlStringFunctions_RefuseWhatAutoCadRefuses(string expression, string message)
        {
            LispResult result = new LispInterpreter().Run(expression);

            Assert.Equal("; error: " + message, result.Output);
            Assert.True(result.Failed);
            Assert.Equal(message, result.Error);
        }

        [Theory]
        [InlineData("(vl-string->list \"abc\")", "(97 98 99)")] // AutoCAD 2021: (97 98 99) (r2)
        [InlineData("(vl-string->list \"\")", "nil")] // AutoCAD 2021: nil (r2)
        [InlineData("(vl-string->list (chr 233))", "(233)")] // AutoCAD 2021: (233) (r2)
        [InlineData("(vl-string->list (chr 273))", "(273)")] // AutoCAD 2021: (273) (r2)
        [InlineData("(vl-string->list \"\\U+0111x\")", "(273 120)")] // AutoCAD 2021: (273 120) (r5)
        [InlineData("(vl-string->list \"a\\U+00E9\")", "(97 233)")] // AutoCAD 2021: (97 233) (r5)
        [InlineData("(vl-string->list \"a\\U+00e9\")", "(97 233)")] // ours: D106, the hex digits in either case, as the printer reads them
        [InlineData("(vl-string->list (strcat (chr 92) \"U+011\"))", "(92 85 43 48 49 49)")] // ours: D106, fewer than four hex digits is no escape
        [InlineData("(vl-list->string '(97 98 99))", "\"abc\"")] // AutoCAD 2021: "abc" (r2)
        [InlineData("(vl-list->string nil)", "\"\"")] // AutoCAD 2021: "" (r2)
        [InlineData("(vl-list->string '(97 0 98))", "\"ab\"")] // AutoCAD 2021: "ab" (r2)
        [InlineData("(strlen (vl-list->string '(273 233)))", "2")] // AutoCAD 2021: 2 (r2)
        [InlineData("(vl-string->list (vl-list->string '(273 233)))", "(273 233)")] // ours: D106, the codes come back
        [InlineData("(strlen (vl-list->string (vl-string->list \"\\U+0111x\")))", "2")] // AutoCAD 2021: 2 (r5)
        [InlineData("(vl-string->list (vl-list->string '(97 65535 65536 -1 98)))", "(97 65535 98)")] // ours: D106, a code outside 1 to 65535 is left out
        public void VlStringToListAndVlListToString_ReadAndMakeUnicodeCodes(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(strlen (chr 273))", "1")] // AutoCAD 2021: 1 (r2)
        [InlineData("(ascii (chr 273))", "273")] // AutoCAD 2021: 273 (r2)
        [InlineData("(strlen (chr 256))", "1")] // AutoCAD 2021: 1 (r5)
        [InlineData("(ascii (chr 256))", "256")] // AutoCAD 2021: 256 (r5)
        [InlineData("(strlen (chr 65535))", "1")] // AutoCAD 2021: a character, 65535 is the last code (r5)
        [InlineData("(ascii (chr 65535))", "65535")] // ours: D106
        [InlineData("(strlen (chr 65536))", "0")] // AutoCAD 2021: 0 (r5)
        [InlineData("(strlen (chr 0))", "0")] // AutoCAD 2021: 0, "" (L1 D25 stays)
        [InlineData("(strlen (chr -1))", "0")] // ours: D106, any other code is the empty string
        [InlineData("(strlen \"\\U+0111x\")", "8")] // AutoCAD 2021: 8, the escape is seven characters (r5)
        [InlineData("(ascii \"\\U+0111\")", "92")] // AutoCAD 2021: 92 (r5)
        [InlineData("(vl-string-search \"x\" \"\\U+0111x\")", "7")] // AutoCAD 2021: 7 (r5)
        [InlineData("(substr \"\\U+0111x\" 2)", "\"U+0111x\"")] // AutoCAD 2021: "U+0111x" (r5)
        [InlineData("(vl-string->list (vl-string-subst \"y\" \"x\" \"\\U+0111x\"))", "(273 121)")] // AutoCAD 2021: "y" after the character, as the console shows it (r5)
        [InlineData("(vl-string->list (vl-string-translate \"x\" \"y\" \"\\U+0111x\"))", "(273 121)")] // AutoCAD 2021: the same (r5)
        [InlineData("(vl-string-search (chr 273) (strcat \"a\" (chr 273)))", "1")] // AutoCAD 2021: 1 (r2)
        [InlineData("(ascii (strcase (chr 273)))", "272")] // AutoCAD 2021: 272 (r7)
        [InlineData("(ascii (strcase (chr 272) T))", "273")] // AutoCAD 2021: 273 (r7)
        [InlineData("(ascii (strcase (chr 233)))", "201")] // AutoCAD 2021: 201 (r7)
        public void AStringIsMadeOfUtf16Characters_AndTheEscapeIsSevenOfThem(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }
    }
}
