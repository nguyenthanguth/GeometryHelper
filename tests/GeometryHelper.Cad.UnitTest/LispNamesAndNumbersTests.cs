using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>snvalid</c>, <c>acad_strlsort</c>, <c>xstrcase</c>, <c>ver</c>, <c>vl-infp</c> and <c>vl-nanp</c> as AutoCAD 2021's
    /// accoreconsole answered them (probes g3, g3b, g4, g4b and g4c of the C4a measurements). Each row is an expression and the
    /// text AutoCAD printed for it, an error as the message after <c>; error: </c>.
    /// </summary>
    [Collection("LispSerial")]
    public class LispNamesAndNumbersTests
    {
        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        #region snvalid

        [Theory]
        [InlineData("(snvalid \"abc\")", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a b\")", "T")] // AutoCAD 2021 (g3): T, a space inside is fine
        [InlineData("(snvalid \"a$b\")", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a_b-c#d\")", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a'b\")", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a[b]{c}!@%^&(d)+~\")", "T")] // AutoCAD 2021 (g3): each of them T on its own
        [InlineData("(snvalid \"0\")", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \" \")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a \")", "nil")] // AutoCAD 2021 (g3): nil, a space at the end
        [InlineData("(snvalid \"  a\")", "nil")] // AutoCAD 2021 (g3): nil, a space at the start
        [InlineData("(snvalid \"a,b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a;b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a:b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a=b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a<b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a>b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a/b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a?b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a*b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a\\\"b\")", "nil")] // AutoCAD 2021 (g3): nil, a quote
        [InlineData("(snvalid (strcat \"a\" (chr 96) \"b\"))", "nil")] // AutoCAD 2021 (g3b): code 96 is refused
        [InlineData("(snvalid (strcat \"a\" (chr 92) \"b\"))", "nil")] // AutoCAD 2021 (g3b): code 92 is refused
        [InlineData("(snvalid (strcat \"a\" (chr 9) \"b\"))", "nil")] // AutoCAD 2021 (g3b): a control character is refused
        [InlineData("(snvalid (strcat \"a\" (chr 127) \"b\"))", "nil")] // AutoCAD 2021 (g3b): 127 is refused
        [InlineData("(snvalid (strcat \"a\" (chr 140) \"b\"))", "nil")] // AutoCAD 2021 (g3b): 128 to 159 are refused
        [InlineData("(snvalid (strcat \"a\" (chr 233) \"b\"))", "T")] // AutoCAD 2021 (g3b): 160 to 255 are fine
        public void SnvalidWithoutItsSecondArgument_AnswersAsAutoCadDoes(string expression, string output) =>
            Assert.Equal(output, Out(expression));

        [Theory]
        [InlineData("(snvalid \"a|b\")", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a|b\" 1)", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a|b\" 0)", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a|b\" nil)", "nil")] // AutoCAD 2021 (g3): nil, nil is 0
        [InlineData("(snvalid \"a|b\" 2)", "T")] // AutoCAD 2021 (g3): T, any number but 0
        [InlineData("(snvalid \"a|b\" -1)", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"a||b\" 1)", "T")] // AutoCAD 2021 (g3b): T
        [InlineData("(snvalid \"a|b|c\" 1)", "T")] // AutoCAD 2021 (g3): T
        [InlineData("(snvalid \"|a\" 1)", "nil")] // AutoCAD 2021 (g3): nil, not at the start
        [InlineData("(snvalid \"a|\" 1)", "nil")] // AutoCAD 2021 (g3b): nil, not at the end
        [InlineData("(snvalid \"|\" 1)", "nil")] // AutoCAD 2021 (g3): nil
        [InlineData("(snvalid \"a|b\" T)", "bad argument type: fixnump: T")] // AutoCAD 2021 (g3): ; error: bad argument type: fixnump: T
        [InlineData("(snvalid \"a|b\" 1.0)", "bad argument type: fixnump: 1.0")] // AutoCAD 2021 (g3)
        [InlineData("(snvalid \"a|b\" \"1\")", "bad argument type: fixnump: \"1\"")] // AutoCAD 2021 (g3)
        [InlineData("(snvalid 5)", "bad argument type: stringp 5")] // AutoCAD 2021 (g3)
        [InlineData("(snvalid nil)", "bad argument type: stringp nil")] // AutoCAD 2021 (g3)
        [InlineData("(snvalid)", "too few arguments")] // AutoCAD 2021 (g3)
        [InlineData("(snvalid \"a\" 1 2)", "too many arguments")] // AutoCAD 2021 (g3)
        public void SnvalidWithTheBarFlag_AnswersAsAutoCadDoes(string expression, string output)
        {
            string text = Out(expression);
            Assert.Equal(output, text.StartsWith("; error: ") ? text.Substring("; error: ".Length) : text);
        }

        #endregion

        #region acad_strlsort, xstrcase and ver

        [Theory]
        [InlineData("(acad_strlsort '(\"b\" \"a\" \"C\" \"c\" \"A\"))", "(\"A\" \"a\" \"b\" \"c\" \"C\")")] // AutoCAD 2021 (g3b): the same words in the reverse of the order given
        [InlineData("(acad_strlsort '(\"a2\" \"a10\" \"a1\" \"A1\"))", "(\"A1\" \"a1\" \"a10\" \"a2\")")] // AutoCAD 2021 (g3b): no natural order of numbers
        [InlineData("(acad_strlsort '(\"10\" \"2\" \"1\" \"20\"))", "(\"1\" \"10\" \"2\" \"20\")")] // AutoCAD 2021 (g3b)
        [InlineData(
            "(acad_strlsort '(\"ab\" \"Ab\" \"aB\" \"AB\" \"a\" \"A\"))",
            "(\"A\" \"a\" \"AB\" \"aB\" \"Ab\" \"ab\")"
        )] // AutoCAD 2021 (g3b)
        [InlineData(
            "(acad_strlsort '(\"\" \"a\" \" \" \"_\" \"-\" \"1\" \"9\" \"A\" \"a\" \"a b\" \"ab\" \"a-b\" \"a_b\" \"a1\" \"a b1\"))",
            "(\"\" \" \" \"-\" \"1\" \"9\" \"_\" \"a\" \"A\" \"a\" \"a b\" \"a b1\" \"a-b\" \"a1\" \"a_b\" \"ab\")"
        )] // AutoCAD 2021 (g3b): lower-case codes, so _ stands between 9 and a
        [InlineData("(acad_strlsort '(\"b\" \"a\" \"b\"))", "(\"a\" \"b\" \"b\")")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort '(\"x\"))", "(\"x\")")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort nil)", "nil")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort '(\"b\" 5))", "nil")] // AutoCAD 2021 (g3b): nil for a list of more than strings
        [InlineData("(acad_strlsort '(\"b\" nil))", "nil")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort 5)", "; error: bad argument type: listp 5")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort \"a\")", "; error: bad argument type: listp \"a\"")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort '(\"b\" \"a\") 1)", "; error: too many arguments")] // AutoCAD 2021 (g3b)
        [InlineData("(acad_strlsort)", "; error: too few arguments")] // AutoCAD 2021 (g3b)
        [InlineData("(xstrcase \"Hello World\")", "\"HELLO WORLD\"")] // AutoCAD 2021 (g3b): upper case
        [InlineData("(xstrcase \"ABC\")", "\"ABC\"")] // AutoCAD 2021 (g3b)
        [InlineData("(xstrcase \"Hello World\" 1)", "; error: too many arguments")] // AutoCAD 2021 (g3b): no second argument
        [InlineData("(xstrcase 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g3b)
        [InlineData("(xstrcase)", "; error: too few arguments")] // AutoCAD 2021 (g3b)
        [InlineData("(ver)", "\"Visual LISP 2021 (en)\"")] // AutoCAD 2021 (g3): "Visual LISP 2021 (en)"
        [InlineData("(type (ver))", "STR")] // AutoCAD 2021 (g3): STR
        public void StringFunctions_AnswerAsAutoCadDoes(string expression, string output) =>
            Assert.Equal(output, Out(expression));

        #endregion

        #region vl-infp and vl-nanp

        [Theory]
        [InlineData("(vl-infp 1.0)", "nil")] // AutoCAD 2021 (g4): nil
        [InlineData("(vl-infp 1)", "nil")] // AutoCAD 2021 (g4): nil
        [InlineData("(vl-infp 1e308)", "nil")] // AutoCAD 2021 (g4): nil
        [InlineData("(vl-infp (* 1e308 10))", "T")] // AutoCAD 2021 (g4): T, (* 1e308 10) is inf.0
        [InlineData("(vl-infp (expt 10.0 400))", "T")] // AutoCAD 2021 (g4): T
        [InlineData("(vl-infp (- (expt 10.0 400)))", "T")] // AutoCAD 2021 (g4b): T for minus infinity
        [InlineData("(vl-infp (- (expt 10.0 400) (expt 10.0 400)))", "nil")] // AutoCAD 2021 (g4b): nil for -nan(ind).0
        [InlineData("(vl-nanp 1.0)", "nil")] // AutoCAD 2021 (g4): nil
        [InlineData("(vl-nanp 1)", "nil")] // AutoCAD 2021 (g4): nil
        [InlineData("(vl-nanp (- (expt 10.0 400) (expt 10.0 400)))", "nil")] // AutoCAD 2021 (g4b): nil even for -nan(ind).0
        [InlineData("(vl-nanp (expt 10.0 400))", "nil")] // AutoCAD 2021 (g4b)
        [InlineData("(vl-infp nil)", "; error: bad argument type: numberp: nil")] // AutoCAD 2021 (g4): the colon after numberp
        [InlineData("(vl-infp \"a\")", "; error: bad argument type: numberp: \"a\"")] // AutoCAD 2021 (g4)
        [InlineData("(vl-nanp nil)", "; error: bad argument type: numberp: nil")] // AutoCAD 2021 (g4)
        [InlineData("(vl-nanp \"a\")", "; error: bad argument type: numberp: \"a\"")] // AutoCAD 2021 (g4)
        [InlineData("(vl-infp T)", "; error: bad argument type: numberp: T")] // AutoCAD 2021 (g4c)
        [InlineData("(vl-infp)", "; error: too few arguments")] // AutoCAD 2021 (g4)
        [InlineData("(vl-infp 1 2)", "; error: too many arguments")] // AutoCAD 2021 (g4)
        [InlineData("(vl-nanp)", "; error: too few arguments")] // AutoCAD 2021 (g4)
        public void TheTwoNumberTests_AnswerAsAutoCadDoes(string expression, string output) =>
            Assert.Equal(output, Out(expression));

        #endregion
    }
}
