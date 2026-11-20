using System.Text.RegularExpressions;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// How the interpreter writes values and what it puts on the command line, against AutoCAD 2021's accoreconsole
    /// (probes p1 and p4). Each row of a theory is an expression and the text AutoCAD printed after the
    /// <c>Command: </c> echo: what the forms wrote with <c>princ</c> and its kin, then the value in <c>prin1</c> form on the
    /// same line.
    /// </summary>
    public class LispPrintingTests
    {
        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        [Theory]
        [InlineData("(progn 1)", "1")] // AutoCAD 2021: 1
        [InlineData("(progn -5)", "-5")] // AutoCAD 2021: -5
        [InlineData("(progn 2147483647)", "2147483647")] // AutoCAD 2021: 2147483647
        [InlineData("(progn 2147483648)", "2.14748e+09")] // AutoCAD 2021: 2.14748e+09
        [InlineData("(progn 1.0)", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(progn 0.1)", "0.1")] // AutoCAD 2021: 0.1
        [InlineData("(progn -0.0)", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(/ 1.0 3)", "0.333333")] // AutoCAD 2021: 0.333333
        [InlineData("(progn 1e20)", "1.0e+20")] // AutoCAD 2021: 1.0e+20
        [InlineData("(progn 1.23456789012e-5)", "1.23457e-05")] // AutoCAD 2021: 1.23457e-05
        [InlineData("(progn 100000.0)", "100000.0")] // AutoCAD 2021: 100000.0
        [InlineData("(progn 1234567.0)", "1.23457e+06")] // AutoCAD 2021: 1.23457e+06
        [InlineData("(progn 123456.789)", "123457.0")] // AutoCAD 2021: 123457.0
        [InlineData("(sqrt 2)", "1.41421")] // AutoCAD 2021: 1.41421
        [InlineData("(progn 3.0e15)", "3.0e+15")] // AutoCAD 2021: 3.0e+15
        [InlineData("(progn 0.0000123)", "1.23e-05")] // AutoCAD 2021: 1.23e-05
        [InlineData("(exp 1)", "2.71828")] // AutoCAD 2021: 2.71828
        [InlineData("(atan 1)", "0.785398")] // AutoCAD 2021: 0.785398
        [InlineData("(atan 1 2)", "0.463648")] // AutoCAD 2021: 0.463648
        [InlineData("(sin 1e20)", "-0.645251")] // AutoCAD 2021: -0.645251
        [InlineData("(exp 1000)", "inf.0")] // AutoCAD 2021: inf.0
        [InlineData("(progn \"abc\")", "\"abc\"")] // AutoCAD 2021: "abc"
        [InlineData(@"(progn ""a\""b\\c\nd\te"")", @"""a\""b\\c\nd\te""")] // AutoCAD 2021: "a\"b\\c\nd\te"
        [InlineData("(progn '(1 2 3))", "(1 2 3)")] // AutoCAD 2021: (1 2 3)
        [InlineData("(progn '(1 . 2))", "(1 . 2)")] // AutoCAD 2021: (1 . 2)
        [InlineData("(progn '(1 (2 . 3) \"x\" 4.5 nil t))", "(1 (2 . 3) \"x\" 4.5 nil T)")] // AutoCAD 2021: (1 (2 . 3) "x" 4.5 nil T)
        [InlineData("(progn 'abc)", "ABC")] // AutoCAD 2021: ABC
        [InlineData("(progn 'AbC)", "ABC")] // AutoCAD 2021: ABC
        [InlineData("(progn t)", "T")] // AutoCAD 2021: T
        [InlineData("(progn nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(progn '())", "nil")] // AutoCAD 2021: nil
        [InlineData("(setq x '(1 . (2 3)))", "(1 2 3)")] // AutoCAD 2021: (1 2 3)
        [InlineData("(progn '(quote a))", "(QUOTE A)")] // AutoCAD 2021: (QUOTE A)
        [InlineData("(progn ''a)", "(QUOTE A)")] // AutoCAD 2021: (QUOTE A)
        [InlineData("(progn '(function a))", "(FUNCTION A)")] // AutoCAD 2021: (FUNCTION A)
        [InlineData("(vl-princ-to-string \"a\")", "\"a\"")] // AutoCAD 2021: "a"
        [InlineData("(vl-prin1-to-string \"a\")", "\"\\\"a\\\"\"")] // AutoCAD 2021: "\"a\""
        [InlineData("(vl-princ-to-string '(1 \"a\" 2.5))", "\"(1 a 2.5)\"")] // AutoCAD 2021: "(1 a 2.5)"
        [InlineData("(vl-prin1-to-string 1.5)", "\"1.5\"")] // AutoCAD 2021: "1.5"
        [InlineData("(vl-prin1-to-string 123456.789)", "\"123457.0\"")] // AutoCAD 2021: "123457.0"
        public void AValueIsWrittenAsAutoCadWritesIt(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(princ \"a\\nb\")", "a\nb\"a\\nb\"")] // AutoCAD 2021: the lines a and b"a\nb"
        [InlineData("(prin1 \"a\\nb\")", "\"a\\nb\"\"a\\nb\"")] // AutoCAD 2021: "a\nb""a\nb"
        [InlineData("(print \"x\")", "\n\"x\" \"x\"")] // AutoCAD 2021: a blank line, then "x" "x"
        [InlineData("(princ '(1 \"a\" 2.5))", "(1 a 2.5)(1 \"a\" 2.5)")] // AutoCAD 2021: (1 a 2.5)(1 "a" 2.5)
        [InlineData("(prin1 'x)", "XX")] // AutoCAD 2021: XX
        [InlineData("(princ 1.0)", "1.01.0")] // AutoCAD 2021: 1.01.0
        [InlineData("(princ 0.0000123)", "1.23e-051.23e-05")] // AutoCAD 2021: 1.23e-051.23e-05
        [InlineData("(princ 123456.789)", "123457.0123457.0")] // AutoCAD 2021: 123457.0123457.0
        [InlineData("(prin1 1e20)", "1.0e+201.0e+20")] // AutoCAD 2021: 1.0e+201.0e+20
        [InlineData("(princ)", "")] // AutoCAD 2021: nothing
        [InlineData("(prompt \"pr\")", "prnil")] // AutoCAD 2021: prnil
        [InlineData("(terpri)", "\nnil")] // AutoCAD 2021: a blank line, then nil
        [InlineData("(princ 'a)", "AA")] // AutoCAD 2021: AA
        [InlineData("(princ nil)", "nilnil")] // AutoCAD 2021: nilnil
        [InlineData("(prin1)", "")] // AutoCAD 2021: nothing
        [InlineData("(print)", "")] // AutoCAD 2021: nothing
        [InlineData("(print 'a)", "\nA A")] // AutoCAD 2021: a blank line, then A A
        [InlineData("(princ \"\\\\\")", "\\\"\\\\\"")] // AutoCAD 2021: \"\\"
        [InlineData("(princ \"\\r\")", "\r\"\\r\"")] // AutoCAD 2021: "\r" after a carriage return
        [InlineData("(princ \"\\e[\")", "\u001b[\"\\e[\"")] // AutoCAD 2021: the escape, [, then "\e["
        [InlineData("(princ (chr 7))", "\u0007\"\\007\"")] // AutoCAD 2021: "\007" after the bell
        [InlineData("(progn (prin1 '(a \"b\\\\c\" 1.5 (2 . 3))) (princ))", "(A \"b\\\\c\" 1.5 (2 . 3))")] // AutoCAD 2021: (A "b\\c" 1.5 (2 . 3))
        [InlineData("(progn (princ \"x\") (princ))", "x")] // AutoCAD 2021: x
        [InlineData("(progn (princ \"x\") (terpri) (princ))", "x\n")] // AutoCAD 2021: x, then the line ends
        public void WhatTheFormsWriteComesBeforeTheValue(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void AStringWithControlCharacters_IsWrittenInOctalAndEscapes()
        {
            // AutoCAD 2021: "a\001\e\177<byte 128>b" twice, and "\t" twice. A character from 128 up is written as it is.
            Assert.Equal(
                "\"a\\001\\e\\177\u0080b\"\"a\\001\\e\\177\u0080b\"",
                Out("(prin1 (strcat \"a\" (chr 1) (chr 27) (chr 127) (chr 128) \"b\"))")
            );
            Assert.Equal("\"\\t\"\"\\t\"", Out("(prin1 (chr 9))"));
        }

        [Theory]
        [InlineData(@"(princ ""\U+0041"")", "A\"A\"")] // AutoCAD 2021: the console shows \U+0041 as A: A"A"
        [InlineData(@"(princ ""\\U+0041"")", "A\"A\"")] // AutoCAD 2021: (princ "\\U+0041") A"A"
        [InlineData(@"(prin1 ""\U+0041"")", "\"A\"\"A\"")] // AutoCAD 2021: "A""A"
        [InlineData(@"(prin1 ""a\U+1EC5b"")", "\"aễb\"\"aễb\"")] // AutoCAD 2021: the console shows \U+1EC5 as that character
        [InlineData(@"(princ '(""\U+00e1"" 1))", "(á 1)(\"á\" 1)")] // AutoCAD 2021: also inside a list, either case of the hex digits
        [InlineData(@"(prompt ""\U+0041"")", "Anil")] // AutoCAD 2021: prompt shows it the same way
        [InlineData(@"(princ ""\U+12"")", "UU+12\"UU+12\"")] // AutoCAD 2021: \U+12 is UU+12, which is shown as it is
        public void AUnicodeEscapeInAString_IsShownAsTheCharacterOnTheCommandLine(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void TheValueOfAStringWithAUnicodeEscape_KeepsItsSevenCharacters()
        {
            // AutoCAD 2021: (strlen "\U+0041") 7   — only what is written to the command line shows the character
            var lisp = new LispInterpreter();
            lisp.Run(@"(setq s ""\U+0041"")");

            Assert.Equal("7", lisp.Run("(strlen s)").Output);
            Assert.Equal(@"\U+0041", lisp.GetValue("s"));
        }

        [Fact]
        public void Princ_WritesAStringAsItIsInsideAList()
        {
            // AutoCAD 2021: (princ '(1 "a" 2.5)) writes (1 a 2.5)
            Assert.Equal("(1 a 2.5)", LispInterpreter.Print(new LispInterpreter().Read("(1 \"a\" 2.5)"), false));
            Assert.Equal("(1 \"a\" 2.5)", LispInterpreter.Print(new LispInterpreter().Read("(1 \"a\" 2.5)"), true));
        }

        [Fact]
        public void ANotANumber_PrintsAsAutoCadPrintsIt()
        {
            // AutoCAD 2021: (setq nan (- (* 1e308 10) (* 1e308 10))) -nan(ind).0 ; (* big 10) inf.0 ; (- (* big 10)) -inf.0
            var lisp = new LispInterpreter();

            Assert.Equal("1.0e+308", lisp.Run("(setq big 1e308)").Output);
            Assert.Equal("inf.0", lisp.Run("(* big 10)").Output);
            Assert.Equal("-inf.0", lisp.Run("(- (* big 10))").Output);
            Assert.Equal("-nan(ind).0", lisp.Run("(setq nan (- (* 1e308 10) (* 1e308 10)))").Output);
        }

        [Theory]
        [InlineData(0.5, "0.5")]
        [InlineData(999999.0, "999999.0")]
        [InlineData(1000000.0, "1.0e+06")]
        [InlineData(0.0001, "0.0001")]
        [InlineData(0.00001, "1.0e-05")]
        [InlineData(-1234.5, "-1234.5")]
        [InlineData(999999.5, "1.0e+06")]
        [InlineData(1.5e300, "1.5e+300")]
        public void ARealIsWrittenWithSixDigits_AsCsPercentG(double value, string printed)
        {
            // %g with six digits, then .0 where the mantissa has no point: the same rule as the measured 100000.0 and 1.23457e+06.
            Assert.Equal(printed, LispInterpreter.Print(value, true));
        }

        [Fact]
        public void AFunction_PrintsAsASubrWithAnAddressAndItsName()
        {
            // AutoCAD 2021: (lambda (x) x) #<SUBR @000001d38254e2a0 -lambda->   (progn car) #<SUBR @000001d3821070e8 CAR>
            var lisp = new LispInterpreter();

            Assert.Matches("^#<SUBR @[0-9a-f]{16} -lambda->$", Out("(lambda (x) x)"));
            Assert.Matches("^#<SUBR @[0-9a-f]{16} CAR>$", Out("(progn car)"));
            Assert.Equal("FF", lisp.Run("(defun ff (a / b) (setq b a) b)").Output);
            Assert.Matches("^#<SUBR @[0-9a-f]{16} FF>$", lisp.Run("(progn ff)").Output);
        }

        [Fact]
        public void TheSameFunction_PrintsWithTheSameAddress_AndTwoFunctionsWithTwo()
        {
            var lisp = new LispInterpreter();
            string first = lisp.Run("(progn car)").Output;

            Assert.Equal(first, lisp.Run("(progn car)").Output);
            Assert.NotEqual(first, lisp.Run("(progn cdr)").Output.Replace("CDR", "CAR"));
        }

        [Fact]
        public void AFunctionOfALambda_IsWrappedAsAQuoteList()
        {
            // AutoCAD 2021: (function (lambda (x) x)) (quote #<SUBR @000001d22268e278 -lambda->)   (function car) CAR   (function nosuch) NOSUCH
            Assert.Matches(@"^\(quote #<SUBR @[0-9a-f]{16} -lambda->\)$", Out("(function (lambda (x) x))"));
            Assert.Equal("CAR", Out("(function car)"));
            Assert.Equal("NOSUCH", Out("(function nosuch)"));
        }

        [Fact]
        public void TheWrappedLambda_IsAFunctionToMapcarAndApply()
        {
            var lisp = new LispInterpreter();

            Assert.Equal("(1 4 9)", lisp.Run("(mapcar (function (lambda (x) (* x x))) '(1 2 3))").Output);
            Assert.Equal("8", lisp.Run("(apply (function (lambda (x) (* x 2))) '(4))").Output);
        }

        [Fact]
        public void AnEntityName_PrintsWithItsHandle()
        {
            // AutoCAD 2021: (entlast) <Entity name: 1d21fb9b8d0>
            var lisp = new LispInterpreter();
            lisp.SetValue("e", new LispEntityName("1d21fb9b8d0"));

            Assert.Equal("<Entity name: 1d21fb9b8d0>", lisp.Run("e").Output);
            Assert.Equal("ENAME", lisp.Run("(type e)").Output);
            Assert.Equal("T", lisp.Run("(eq e e)").Output);
            lisp.SetValue("f", new LispEntityName("1d21fb9b8d0"));
            Assert.Equal("T", lisp.Run("(eq e f)").Output);
        }

        [Fact]
        public void ASelectionSet_PrintsWithANumber_AndIsAPickset()
        {
            // AutoCAD 2021: (ssadd) <Selection set: 5>   (type (ssadd)) PICKSET
            var lisp = new LispInterpreter();
            var set = new LispSelectionSet(new[] { "1A", "1B" });
            lisp.SetValue("ss", set);

            Assert.Matches("^<Selection set: [0-9]+>$", lisp.Run("ss").Output);
            Assert.Equal("PICKSET", lisp.Run("(type ss)").Output);
            Assert.Equal(new[] { "1A", "1B" }, set.Handles);
        }

        [Fact]
        public void ACatchAllError_PrintsAsTheCatchAllApplyError()
        {
            // AutoCAD 2021: (vl-catch-all-apply '/ '(1 0)) #<%catch-all-apply-error%>
            Assert.Equal("#<%catch-all-apply-error%>", Out("(vl-catch-all-apply '/ '(1 0))"));
        }

        [Theory]
        [InlineData("(type 1)", "INT")] // AutoCAD 2021: INT
        [InlineData("(type 1.0)", "REAL")] // AutoCAD 2021: REAL
        [InlineData("(type \"a\")", "STR")] // AutoCAD 2021: STR
        [InlineData("(type 'a)", "SYM")] // AutoCAD 2021: SYM
        [InlineData("(type '(1))", "LIST")] // AutoCAD 2021: LIST
        [InlineData("(type car)", "SUBR")] // AutoCAD 2021: SUBR
        [InlineData("(type (lambda (x) x))", "SUBR")] // AutoCAD 2021: SUBR
        [InlineData("(type nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(type t)", "SYM")] // AutoCAD 2021: SYM
        [InlineData("(type 'nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(type (read \"1e400\"))", "REAL")] // AutoCAD 2021: REAL
        [InlineData("(type (car (list car)))", "SUBR")] // AutoCAD 2021: SUBR
        [InlineData("(type \"\")", "STR")] // AutoCAD 2021: STR
        [InlineData("(type (vl-catch-all-apply '/ '(1 0)))", "VL-CATCH-ALL-APPLY-ERROR")] // AutoCAD 2021: VL-CATCH-ALL-APPLY-ERROR
        public void TheTypeOfAValue_IsTheSymbolAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void ADefunqFunction_IsAList_AndStillCallable()
        {
            // AutoCAD 2021: (defun-q dq (x) x) DQ   (progn dq) ((X) X)   (defun-q gq (a) (* a 2)) (gq 3) 6   (type gq) LIST   (defun-q-list-ref 'gq) ((A) (* A 2))
            var lisp = new LispInterpreter();

            Assert.Equal("DQ", lisp.Run("(defun-q dq (x) x)").Output);
            Assert.Equal("((X) X)", lisp.Run("(progn dq)").Output);
            Assert.Equal("GQ", lisp.Run("(defun-q gq (a) (* a 2))").Output);
            Assert.Equal("6", lisp.Run("(gq 3)").Output);
            Assert.Equal("LIST", lisp.Run("(type gq)").Output);
            Assert.Equal("((A) (* A 2))", lisp.Run("(defun-q-list-ref 'gq)").Output);
        }

        [Fact]
        public void TheEmptySymbol_PrintsAsNothing_AndIsWhatPrincAndPrin1AndPrintGiveWithNoArgument()
        {
            // AutoCAD 2021: (princ) (prin1) (print) (vl-load-com) echo nothing
            var lisp = new LispInterpreter();

            Assert.Equal("", LispInterpreter.Print(lisp.Run("(princ)").Value, true));
            Assert.Equal("", LispInterpreter.Print(lisp.Run("(prin1)").Value, false));
            Assert.Equal("", lisp.Run("(vl-load-com)").Output);
        }

        [Fact]
        public void ARunOfSeveralForms_WritesWhatEachWrote_AndEchoesTheLastValue()
        {
            // Not measured: the command line takes one form at a time. Several in one text write in order, and the last value is echoed.
            var lisp = new LispInterpreter();
            LispResult result = lisp.Run("(princ \"a\") (princ \"b\") 3");

            Assert.Equal("ab3", result.Output);
            Assert.Equal(3, result.Value);
            Assert.False(result.Failed);
            Assert.Null(result.Error);
        }
    }
}
