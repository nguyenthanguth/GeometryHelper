using System;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the interpreter computes, against AutoCAD 2021's accoreconsole (probes p1, p2 and p4): the numbers, strings, lists,
    /// comparisons and control forms, and the corners in which AutoCAD is not like other Lisps (integers that wrap at 32
    /// bits, <c>(and 1 2)</c> giving T, <c>(strlen "abc" "de")</c> giving 5). Each row is an expression and the text AutoCAD
    /// printed for it; a test that needs a state runs its expressions in order on one interpreter.
    /// </summary>
    [Collection("LispSerial")]
    public class LispEvaluatorTests
    {
        private readonly ITestOutputHelper _output;

        public LispEvaluatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        private static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        #region Numbers

        [Theory]
        [InlineData("(+ 2147483647 1)", "-2147483648")] // AutoCAD 2021: -2147483648
        [InlineData("(* 65536 65536)", "0")] // AutoCAD 2021: 0
        [InlineData("(- -2147483647 2)", "2147483647")] // AutoCAD 2021: 2147483647
        [InlineData("(abs -2147483648)", "2.14748e+09")] // AutoCAD 2021: 2.14748e+09
        [InlineData("(1+ 2147483647)", "-2147483648")] // AutoCAD 2021: -2147483648
        [InlineData("(- -2147483648)", "2.14748e+09")] // AutoCAD 2021: 2.14748e+09
        [InlineData("(expt 2 31)", "-2147483648")] // AutoCAD 2021: -2147483648
        [InlineData("(lsh 1 31)", "-2147483648")] // AutoCAD 2021: -2147483648
        [InlineData("(lsh 1 32)", "1")] // AutoCAD 2021: 1
        [InlineData("(lsh -1 -1)", "2147483647")] // AutoCAD 2021: 2147483647
        [InlineData("(lsh 5)", "5")] // AutoCAD 2021: 5
        [InlineData("(lsh)", "0")] // AutoCAD 2021: 0
        [InlineData("(~ 5)", "-6")] // AutoCAD 2021: -6
        [InlineData("(logand 7 12)", "4")] // AutoCAD 2021: 4
        [InlineData("(logior 1 2 4)", "7")] // AutoCAD 2021: 7
        [InlineData("(logand)", "0")] // AutoCAD 2021: 0
        public void AnIntegerHas32Bits_AndWrapsAround(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(/ 7 2)", "3")] // AutoCAD 2021: 3
        [InlineData("(/ -7 2)", "-3")] // AutoCAD 2021: -3
        [InlineData("(/ 7 2.0)", "3.5")] // AutoCAD 2021: 3.5
        [InlineData("(/ 7)", "7")] // AutoCAD 2021: 7
        [InlineData("(- 5)", "-5")] // AutoCAD 2021: -5
        [InlineData("(+)", "0")] // AutoCAD 2021: 0
        [InlineData("(*)", "0")] // AutoCAD 2021: 0
        [InlineData("(-)", "0")] // AutoCAD 2021: 0
        [InlineData("(/)", "0")] // AutoCAD 2021: 0
        [InlineData("(max)", "0")] // AutoCAD 2021: 0
        [InlineData("(rem 7 -2)", "1")] // AutoCAD 2021: 1
        [InlineData("(rem -7 2)", "-1")] // AutoCAD 2021: -1
        [InlineData("(rem 7.5 2)", "1.5")] // AutoCAD 2021: 1.5
        [InlineData("(rem 7)", "7")] // AutoCAD 2021: 7
        [InlineData("(rem)", "0")] // AutoCAD 2021: 0
        [InlineData("(rem 1 2 3)", "1")] // AutoCAD 2021: 1
        [InlineData("(rem 10.0 3 2)", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(expt 2 3)", "8")] // AutoCAD 2021: 8
        [InlineData("(expt 2 0.5)", "1.41421")] // AutoCAD 2021: 1.41421
        [InlineData("(expt 2.0 3)", "8.0")] // AutoCAD 2021: 8.0
        [InlineData("(expt 2 -1)", "0")] // AutoCAD 2021: 0
        [InlineData("(expt 2 1.0)", "2.0")] // AutoCAD 2021: 2.0
        [InlineData("(fix 2.7)", "2")] // AutoCAD 2021: 2
        [InlineData("(fix -2.7)", "-2")] // AutoCAD 2021: -2
        [InlineData("(fix 1e20)", "1.0e+20")] // AutoCAD 2021: 1.0e+20
        [InlineData("(float 2)", "2.0")] // AutoCAD 2021: 2.0
        [InlineData("(gcd 12 18)", "6")] // AutoCAD 2021: 6
        [InlineData("(max 1 2.0)", "2.0")] // AutoCAD 2021: 2.0
        [InlineData("(min 1 2)", "1")] // AutoCAD 2021: 1
        [InlineData("(atan 0 0)", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(atan 1 0)", "1.5708")] // AutoCAD 2021: 1.5708
        [InlineData("(atan -1 0)", "-1.5708")] // AutoCAD 2021: -1.5708
        [InlineData("(sin 0)", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(cos 0)", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(sqrt 4)", "2.0")] // AutoCAD 2021: 2.0
        [InlineData("(log 2.718281828)", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(1+ 1.5)", "2.5")] // AutoCAD 2021: 2.5
        [InlineData("(abs -3.5)", "3.5")] // AutoCAD 2021: 3.5
        [InlineData("(+ 1 2.0)", "3.0")] // AutoCAD 2021: 3.0
        [InlineData("(+ 1.5 1)", "2.5")] // AutoCAD 2021: 2.5
        [InlineData("(- 10 1 2)", "7")] // AutoCAD 2021: 7
        [InlineData("(* 2 2.5)", "5.0")] // AutoCAD 2021: 5.0
        [InlineData("(minusp -1)", "T")] // AutoCAD 2021: T
        [InlineData("(minusp 0)", "nil")] // AutoCAD 2021: nil
        [InlineData("(zerop 0.0)", "T")] // AutoCAD 2021: T
        [InlineData("(numberp \"1\")", "nil")] // AutoCAD 2021: nil
        [InlineData("(numberp 1.5)", "T")] // AutoCAD 2021: T
        [InlineData("(pi)", "; error: bad function: 3.14159")] // pi is a variable, not a function
        [InlineData("(progn pi)", "3.14159")] // AutoCAD 2021: 3.14159
        [InlineData("(eval 'pi)", "3.14159")] // AutoCAD 2021: 3.14159
        public void ANumberFunctionGivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void PiCanBeSetLikeAnyVariable()
        {
            // AutoCAD 2021: (setq pi 3) 3   !pi 3   (setq pi 3.14159265358979) 3.14159
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq pi 3)", "3");
            Expect(lisp, "pi", "3");
            Expect(lisp, "(setq pi 3.14159265358979)", "3.14159");
        }

        #endregion

        #region Comparison

        [Theory]
        [InlineData("(eq \"a\" \"a\")", "T")] // AutoCAD 2021: T
        [InlineData("(equal \"a\" \"a\")", "T")] // AutoCAD 2021: T
        [InlineData("(equal 1.0 1.0001 0.001)", "T")] // AutoCAD 2021: T
        [InlineData("(equal '(1 2.0) '(1 2.0001) 0.01)", "T")] // AutoCAD 2021: T
        [InlineData("(eq '(1) '(1))", "nil")] // AutoCAD 2021: nil
        [InlineData("(= 1 1.0)", "T")] // AutoCAD 2021: T
        [InlineData("(= \"a\" \"a\")", "T")] // AutoCAD 2021: T
        [InlineData("(= \"a\" \"A\")", "nil")] // AutoCAD 2021: nil
        [InlineData("(= 'a 'a)", "T")] // AutoCAD 2021: T
        [InlineData("(/= 1 2 1)", "T")] // AutoCAD 2021: T
        [InlineData("(/= 1 2 3)", "T")] // AutoCAD 2021: T
        [InlineData("(< 1 2 3)", "T")] // AutoCAD 2021: T
        [InlineData("(< 1 3 2)", "nil")] // AutoCAD 2021: nil
        [InlineData("(< \"a\" \"b\")", "T")] // AutoCAD 2021: T
        [InlineData("(< \"B\" \"a\")", "T")] // AutoCAD 2021: T
        [InlineData("(= 1 \"a\")", "nil")] // AutoCAD 2021: nil
        [InlineData("(eq nil '())", "T")] // AutoCAD 2021: T
        [InlineData("(eq 'a 'A)", "T")] // AutoCAD 2021: T
        [InlineData("(equal 2 2.0)", "T")] // AutoCAD 2021: T
        [InlineData("(equal 2 2.0 0.1)", "T")] // AutoCAD 2021: T
        [InlineData("(eq 1 1)", "T")] // AutoCAD 2021: T
        [InlineData("(eq 1.0 1.0)", "T")] // AutoCAD 2021: T
        [InlineData("(eq 'x 'x)", "T")] // AutoCAD 2021: T
        [InlineData("(equal '(1 (2 \"a\")) '(1 (2 \"a\")))", "T")] // AutoCAD 2021: T
        [InlineData("(equal 1 1.0000001)", "nil")] // AutoCAD 2021: nil
        [InlineData("(equal 1.0 1.0000000001)", "nil")] // AutoCAD 2021: nil
        [InlineData("(equal \"a\" 'a)", "nil")] // AutoCAD 2021: nil
        [InlineData("(= 1)", "T")] // AutoCAD 2021: T
        [InlineData("(= 1 1 1.0)", "T")] // AutoCAD 2021: T
        [InlineData("(= 1 1 2)", "nil")] // AutoCAD 2021: nil
        [InlineData("(/= 1)", "T")] // AutoCAD 2021: T
        [InlineData("(/= 1 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(/= 1 2 2)", "nil")] // AutoCAD 2021: nil
        [InlineData("(< 1)", "T")] // AutoCAD 2021: T
        [InlineData("(> 3 2 1)", "T")] // AutoCAD 2021: T
        [InlineData("(>= 3 3 1)", "T")] // AutoCAD 2021: T
        [InlineData("(<= 1 1 2)", "T")] // AutoCAD 2021: T
        [InlineData("(< 1.0 2)", "T")] // AutoCAD 2021: T
        [InlineData("(= nil nil)", "T")] // AutoCAD 2021: T
        [InlineData("(= '(1) '(1))", "nil")] // AutoCAD 2021: nil
        [InlineData("(equal 1.0 1 0)", "T")] // AutoCAD 2021: T
        [InlineData("(equal 1.0 1.0 -1)", "T")] // AutoCAD 2021: T
        public void TwoValuesAreComparedAsAutoCadComparesThem(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void AListIsEqToItself_AndToNoOtherList()
        {
            // AutoCAD 2021: (setq gh1 '(1 2) gh2 gh1) (eq gh1 gh2) T   (eq '(1 2) '(1 2)) nil
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq gh1 '(1 2) gh2 gh1)", "(1 2)");
            Expect(lisp, "(eq gh1 gh2)", "T");
            Expect(lisp, "(eq '(1 2) '(1 2))", "nil");
        }

        [Fact]
        public void AStringMadeByStrcat_IsEqToTheSameText()
        {
            // AutoCAD 2021: (setq s1 (strcat "a" "b")) (eq s1 "ab") T   (eq s1 s1) T   (setq sa "a") (eq sa sa) T
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq s1 (strcat \"a\" \"b\"))", "\"ab\"");
            Expect(lisp, "(eq s1 \"ab\")", "T");
            Expect(lisp, "(eq s1 s1)", "T");
        }

        #endregion

        #region Strings

        [Theory]
        [InlineData("(strcase \"abc\")", "\"ABC\"")] // AutoCAD 2021: "ABC"
        [InlineData("(strcase \"ABC\" T)", "\"abc\"")] // AutoCAD 2021: "abc"
        [InlineData("(strcase \"aBc\" nil)", "\"ABC\"")] // AutoCAD 2021: "ABC"
        [InlineData("(strcase \"aBc\" 0)", "\"abc\"")] // AutoCAD 2021: "abc"
        [InlineData("(substr \"hello\" 2)", "\"ello\"")] // AutoCAD 2021: "ello"
        [InlineData("(substr \"hello\" 2 2)", "\"el\"")] // AutoCAD 2021: "el"
        [InlineData("(substr \"hello\" 10)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(substr \"hello\" 1 0)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(substr \"hello\" 6)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(substr \"hello\" 5 10)", "\"o\"")] // AutoCAD 2021: "o"
        [InlineData("(strlen \"abc\" \"de\")", "5")] // AutoCAD 2021: 5
        [InlineData("(strlen)", "0")] // AutoCAD 2021: 0
        [InlineData("(ascii \"\")", "0")] // AutoCAD 2021: 0
        [InlineData("(ascii \"AB\")", "65")] // AutoCAD 2021: 65
        [InlineData("(ascii \"a\")", "97")] // AutoCAD 2021: 97
        [InlineData("(chr 65)", "\"A\"")] // AutoCAD 2021: "A"
        [InlineData("(chr 0)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(chr 256)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(chr 273)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(atoi \"x\")", "0")] // AutoCAD 2021: 0
        [InlineData("(atoi \"12abc\")", "12")] // AutoCAD 2021: 12
        [InlineData("(atoi \"  -3\")", "-3")] // AutoCAD 2021: -3
        [InlineData("(atoi \"3.9\")", "3")] // AutoCAD 2021: 3
        [InlineData("(atoi \"\")", "0")] // AutoCAD 2021: 0
        [InlineData("(atoi \"+5\")", "5")] // AutoCAD 2021: 5
        [InlineData("(atoi \"99999999999\")", "2147483647")] // AutoCAD 2021: 2147483647
        [InlineData("(atoi \"-99999999999\")", "-2147483647")] // AutoCAD 2021: -2147483647
        [InlineData("(atof \"abc\")", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(atof \"1.5e3\")", "1500.0")] // AutoCAD 2021: 1500.0
        [InlineData("(atof \" 2.5x\")", "2.5")] // AutoCAD 2021: 2.5
        [InlineData("(atof \"1e400\")", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(atof \"\")", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(atof \".5\")", "0.5")] // AutoCAD 2021: 0.5
        [InlineData("(atof \"1,5\")", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(atof \"1e\")", "1.0")] // AutoCAD 2021: 1.0
        [InlineData("(atof \"-\")", "0.0")] // AutoCAD 2021: 0.0
        [InlineData("(itoa -5)", "\"-5\"")] // AutoCAD 2021: "-5"
        [InlineData("(itoa 99999999)", "\"99999999\"")] // AutoCAD 2021: "99999999"
        [InlineData("(strcat \"a\" \"b\" \"c\")", "\"abc\"")] // AutoCAD 2021: "abc"
        [InlineData("(strcat)", "\"\"")] // AutoCAD 2021: ""
        [InlineData("(vl-symbol-name 'a)", "\"A\"")] // AutoCAD 2021: "A"
        public void AStringFunctionGivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        #endregion

        #region Lists

        [Theory]
        [InlineData("(assoc 2 '((1 . a) (2 . b)))", "(2 . B)")] // AutoCAD 2021: (2 . B)
        [InlineData("(assoc 9 '((1 . a)))", "nil")] // AutoCAD 2021: nil
        [InlineData("(member 2 '(1 2 3))", "(2 3)")] // AutoCAD 2021: (2 3)
        [InlineData("(nth 5 '(1 2))", "nil")] // AutoCAD 2021: nil
        [InlineData("(nth 1 '(1 2))", "2")] // AutoCAD 2021: 2
        [InlineData("(last '(1 2))", "2")] // AutoCAD 2021: 2
        [InlineData("(last nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(last '())", "nil")] // AutoCAD 2021: nil
        [InlineData("(reverse '(1 2 3))", "(3 2 1)")] // AutoCAD 2021: (3 2 1)
        [InlineData("(reverse nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(subst 'x 2 '(1 2 3 (2)))", "(1 X 3 (2))")] // AutoCAD 2021: (1 X 3 (2))
        [InlineData("(subst \"x\" \"a\" '(\"a\" \"b\"))", "(\"x\" \"b\")")] // AutoCAD 2021: ("x" "b")
        [InlineData("(append '(1) '(2) '(3 4))", "(1 2 3 4)")] // AutoCAD 2021: (1 2 3 4)
        [InlineData("(append)", "nil")] // AutoCAD 2021: nil
        [InlineData("(append nil nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(append '(1 2) nil '(3))", "(1 2 3)")] // AutoCAD 2021: (1 2 3)
        [InlineData("(length '(1 2))", "2")] // AutoCAD 2021: 2
        [InlineData("(length nil)", "0")] // AutoCAD 2021: 0
        [InlineData("(cons 1 2)", "(1 . 2)")] // AutoCAD 2021: (1 . 2)
        [InlineData("(cons 1 '(2))", "(1 2)")] // AutoCAD 2021: (1 2)
        [InlineData("(cons 1 nil)", "(1)")] // AutoCAD 2021: (1)
        [InlineData("(cadr '(1 2 3))", "2")] // AutoCAD 2021: 2
        [InlineData("(cddr '(1 2 3))", "(3)")] // AutoCAD 2021: (3)
        [InlineData("(caddr '(1 2 3))", "3")] // AutoCAD 2021: 3
        [InlineData("(cadddr '(1 2 3 4))", "4")] // AutoCAD 2021: 4
        [InlineData("(caaaar '((((1)))))", "1")] // AutoCAD 2021: 1
        [InlineData("(cdddr '(1 2 3 4))", "(4)")] // AutoCAD 2021: (4)
        [InlineData("(car nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(cdr nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(car '(1 . 2))", "1")] // AutoCAD 2021: 1
        [InlineData("(cdr '(1 . 2))", "2")] // AutoCAD 2021: 2
        [InlineData("(list)", "nil")] // AutoCAD 2021: nil
        [InlineData("(member 1 '(1 . 2))", "(1 . 2)")] // AutoCAD 2021: (1 . 2)
        [InlineData("(member 1.0 '(1 2))", "(1 2)")] // AutoCAD 2021: (1 2)
        [InlineData("(member \"a\" '(\"a\"))", "(\"a\")")] // AutoCAD 2021: ("a")
        [InlineData("(assoc \"a\" '((\"a\" . 1)))", "(\"a\" . 1)")] // AutoCAD 2021: ("a" . 1)
        [InlineData("(assoc 1.0 '((1 . 2)))", "(1 . 2)")] // AutoCAD 2021: (1 . 2)
        [InlineData("(listp nil)", "T")] // AutoCAD 2021: T
        [InlineData("(listp '(1 . 2))", "T")] // AutoCAD 2021: T
        [InlineData("(listp \"a\")", "nil")] // AutoCAD 2021: nil
        [InlineData("(atom nil)", "T")] // AutoCAD 2021: T
        [InlineData("(atom 'a)", "T")] // AutoCAD 2021: T
        [InlineData("(atom '(1))", "nil")] // AutoCAD 2021: nil
        [InlineData("(atom \"a\")", "T")] // AutoCAD 2021: T
        [InlineData("(vl-consp nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(vl-consp '(1))", "T")] // AutoCAD 2021: T
        [InlineData("(not 0)", "nil")] // AutoCAD 2021: nil
        [InlineData("(null nil)", "T")] // AutoCAD 2021: T
        [InlineData("(null 1)", "nil")] // AutoCAD 2021: nil
        public void AListFunctionGivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(mapcar '+ '(1 2) '(10 20 30))", "(11 22)")] // AutoCAD 2021: (11 22)
        [InlineData("(mapcar '(lambda (x) (* x x)) '(1 2 3))", "(1 4 9)")] // AutoCAD 2021: (1 4 9)
        [InlineData("(mapcar 'car '((1) (2)))", "(1 2)")] // AutoCAD 2021: (1 2)
        [InlineData("(mapcar 'list '(1 2) '(a b))", "((1 A) (2 B))")] // AutoCAD 2021: ((1 A) (2 B))
        [InlineData("(apply '+ '(1 2 3))", "6")] // AutoCAD 2021: 6
        [InlineData("(apply 'max '(1 5 3))", "5")] // AutoCAD 2021: 5
        [InlineData("(apply 'if '(t 1 2))", "1")] // AutoCAD 2021: 1
        [InlineData("(apply '(lambda (x) (* x 2)) '(4))", "8")] // AutoCAD 2021: 8
        [InlineData("(apply 'quote '(a))", "(quote A)")] // AutoCAD 2021: (quote A)
        [InlineData("(eval '(+ 1 2))", "3")] // AutoCAD 2021: 3
        [InlineData("(eval 5)", "5")] // AutoCAD 2021: 5
        [InlineData("(eval \"s\")", "\"s\"")] // AutoCAD 2021: "s"
        [InlineData("(eval nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(eval (read \"(+ 1 2)\"))", "3")] // AutoCAD 2021: 3
        [InlineData("(read \"(a b)\")", "(A B)")] // AutoCAD 2021: (A B)
        public void ApplyMapcarAndEvalWorkAsAutoCadsDo(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void ThousandConses_BuildAListOfThatLength()
        {
            // AutoCAD 2021: (repeat 1000 (setq ghl (cons 1 ghl))) then (length ghl) 1000, and (vl-symbol-value 'ghl) is the list
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq ghl nil)", "nil");
            lisp.Run("(repeat 1000 (setq ghl (cons 1 ghl)))");
            Expect(lisp, "(length ghl)", "1000");
            Assert.Equal("1000", lisp.Run("(length (vl-symbol-value 'ghl))").Output);
        }

        [Fact]
        public void ConsCellsAreNeverChanged_SoASharedListStaysWhatItWas()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq a '(1 2 3) b (cons 0 a) c (subst 9 2 a) d (append a '(4)))");

            Expect(lisp, "a", "(1 2 3)");
            Expect(lisp, "b", "(0 1 2 3)");
            Expect(lisp, "c", "(1 9 3)");
            Expect(lisp, "d", "(1 2 3 4)");
        }

        [Fact]
        public void AtomsFamily_ListsTheSymbolsThatExist()
        {
            // AutoCAD 2021: (atoms-family 1 '("CAR" "NOSUCH")) ("CAR" nil)   (atoms-family 0 '("car")) (CAR)
            // (atoms-family 0 '("ghr" "ghr2" "zzzz")) after the two defuns: (GHR GHR2 nil)
            var lisp = new LispInterpreter();

            Expect(lisp, "(atoms-family 1 '(\"CAR\" \"NOSUCH\"))", "(\"CAR\" nil)");
            Expect(lisp, "(atoms-family 0 '(\"car\"))", "(CAR)");
            lisp.Run("(defun ghr (n) n)");
            lisp.Run("(defun ghr2 (n) n)");
            Expect(lisp, "(atoms-family 0 '(\"ghr\" \"ghr2\" \"zzzz\"))", "(GHR GHR2 nil)");
        }

        #endregion

        #region Control forms and definitions

        [Theory]
        [InlineData("(if nil 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(if nil 1 2)", "2")] // AutoCAD 2021: 2
        [InlineData("(if 0 'yes)", "YES")] // AutoCAD 2021: YES
        [InlineData("(cond)", "nil")] // AutoCAD 2021: nil
        [InlineData("(cond (nil 1) (2))", "2")] // AutoCAD 2021: 2
        [InlineData("(cond ((= 1 1)))", "T")] // AutoCAD 2021: T
        [InlineData("(cond (t))", "T")] // AutoCAD 2021: T
        [InlineData("(cond (nil))", "nil")] // AutoCAD 2021: nil
        [InlineData("(cond (1 2 3))", "3")] // AutoCAD 2021: 3
        [InlineData("(while nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(while (setq w2 nil))", "nil")] // AutoCAD 2021: nil
        [InlineData("(repeat 3 (setq z 1))", "1")] // AutoCAD 2021: 1
        [InlineData("(repeat 0 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(repeat -1 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(repeat 3)", "nil")] // AutoCAD 2021: nil
        [InlineData("(foreach x '(1 2) x)", "2")] // AutoCAD 2021: 2
        [InlineData("(foreach x nil 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(foreach x '(1 2 3))", "nil")] // AutoCAD 2021: nil
        [InlineData("(and)", "T")] // AutoCAD 2021: T
        [InlineData("(or)", "nil")] // AutoCAD 2021: nil
        [InlineData("(and 1 nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(and 1 2)", "T")] // AutoCAD 2021: T
        [InlineData("(or nil 2)", "T")] // AutoCAD 2021: T
        [InlineData("(and nil (car 1))", "nil")] // AutoCAD 2021: nil
        [InlineData("(or 1 (car 1))", "T")] // AutoCAD 2021: T
        [InlineData("(progn)", "nil")] // AutoCAD 2021: nil
        [InlineData("(progn 1 2 3)", "3")] // AutoCAD 2021: 3
        [InlineData("(quote (1 2))", "(1 2)")] // AutoCAD 2021: (1 2)
        [InlineData("((lambda (x) (* x 10)) 5)", "50")] // AutoCAD 2021: 50
        [InlineData("((lambda (x / y) (setq y x) y) 5)", "5")] // AutoCAD 2021: 5
        [InlineData("(boundp 'zzz)", "nil")] // AutoCAD 2021: nil
        [InlineData("(boundp 'car)", "T")] // AutoCAD 2021: T
        [InlineData("(boundp 1)", "nil")] // AutoCAD 2021: nil
        [InlineData("(boundp nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(boundp t)", "T")] // AutoCAD 2021: T
        [InlineData("(set 'q 5)", "5")] // AutoCAD 2021: 5
        [InlineData("(vl-symbolp 'a)", "T")] // AutoCAD 2021: T
        [InlineData("(vl-load-com)", "")] // AutoCAD 2021: nothing
        public void AControlFormGivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-symbolp nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(vl-symbolp t)", "T")] // AutoCAD 2021: T
        [InlineData("(boole 1 12 10)", "8")] // AutoCAD 2021: 8
        [InlineData("(boole 7 12 10)", "14")] // AutoCAD 2021: 14
        [InlineData("(boole 6 5 3 1)", "7")] // AutoCAD 2021: 7
        [InlineData("(boole 2 12)", "12")] // AutoCAD 2021: 12
        [InlineData("(boole 1)", "; error: too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(boole 16 1 2)", "; error: boole arg1 < 0 or > 15: 16")] // AutoCAD 2021: ; error: boole arg1 < 0 or > 15: 16
        [InlineData("(boole 1.0 1 2)", "; error: bad argument type: fixnump: 1.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.0
        [InlineData("(defun-q-list-set 'gq2 5)", "; error: bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5
        [InlineData("(defun-q-list-set \"gq3\" '((x) x))", "; error: bad argument type: symbolp \"gq3\"")] // AutoCAD 2021: ; error: bad argument type: symbolp "gq3"
        public void BooleAndVlSymbolpGiveWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void DefunQListSet_MakesAFunctionOfAList()
        {
            // AutoCAD 2021: (defun-q-list-set 'gq '((x) (* x 5))) GQ, then (gq 2) 10
            var lisp = new LispInterpreter();

            Expect(lisp, "(defun-q-list-set 'gq '((x) (* x 5)))", "GQ");
            Expect(lisp, "(gq 2)", "10");
        }

        [Fact]
        public void AWhileGivesTheLastValueOfItsBody()
        {
            // AutoCAD 2021: (setq i 0) (while (< i 3) (setq i (1+ i)) i) 3
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq i 0)", "0");
            Expect(lisp, "(while (< i 3) (setq i (1+ i)) i)", "3");
        }

        [Fact]
        public void ADefunGivesItsName_AndTheFunctionIsCalled()
        {
            // AutoCAD 2021: (defun ff (a / b) (setq b a) b) FF   (ff 3) 3   (defun f2 (x) (* x 2)) F2   (f2 (f2 3)) 12
            var lisp = new LispInterpreter();

            Expect(lisp, "(defun ff (a / b) (setq b a) b)", "FF");
            Expect(lisp, "(ff 3)", "3");
            Expect(lisp, "(defun f2 (x) (* x 2))", "F2");
            Expect(lisp, "(f2 (f2 3))", "12");
            Assert.Null(lisp.GetValue("b"));
        }

        [Fact]
        public void AFunctionDefinedTwice_IsTheSecondDefinition()
        {
            // AutoCAD 2021: (defun f (x) (* x 2)) (defun f (x) (* x 3)) (f 2) 6
            var lisp = new LispInterpreter();
            lisp.Run("(defun f (x) (* x 2))");
            lisp.Run("(defun f (x) (* x 3))");

            Expect(lisp, "(f 2)", "6");
        }

        [Fact]
        public void ALocalNamedLikeAnArgument_IsTheArgument()
        {
            // AutoCAD 2021: (defun f3 (x / x) x) (f3 1) 1   (defun f4 (/ ) 1) (f4) 1   (defun f11 (a /) a) (f11 1) 1
            var lisp = new LispInterpreter();
            lisp.Run("(defun f3 (x / x) x)");
            lisp.Run("(defun f4 (/ ) 1)");
            lisp.Run("(defun f11 (a /) a)");

            Expect(lisp, "(f3 1)", "1");
            Expect(lisp, "(f4)", "1");
            Expect(lisp, "(f11 1)", "1");
        }

        [Fact]
        public void TheArgumentsAndLocalsOfAFunction_AreGivenBackWhenItReturns()
        {
            // AutoCAD 2021: (setq a 1) ... after (h2 5) fails, a is 1 again
            var lisp = new LispInterpreter();
            lisp.Run("(setq a 1 b 10)");
            lisp.Run("(defun g (a / b) (setq b 99) (+ a b))");

            Expect(lisp, "(g 5)", "104");
            Assert.Equal(1, lisp.GetValue("a"));
            Assert.Equal(10, lisp.GetValue("b"));
        }

        [Fact]
        public void AFunctionSeesTheVariablesOfItsCaller()
        {
            // Dynamic scoping: AutoLISP has no closures; a lambda finds a variable by the binding that is current when it runs.
            var lisp = new LispInterpreter();
            lisp.Run("(defun inner () (* mult 2))");
            lisp.Run("(defun outer (mult) (inner))");

            Expect(lisp, "(outer 21)", "42");
            Expect(lisp, "(inner)", "; error: bad argument type: numberp: nil");
        }

        [Fact]
        public void AForeachVariable_HasItsOldValueBackAfterTheLoop()
        {
            // Not measured: the variable of foreach is given back as a function's argument is.
            var lisp = new LispInterpreter();
            lisp.Run("(setq x 'before)");
            lisp.Run("(foreach x '(1 2 3) x)");

            Expect(lisp, "x", "BEFORE");
        }

        [Fact]
        public void ADefunCanOverrideABuiltIn_AndSetqToNilRemovesIt()
        {
            // AutoCAD 2021: (setq savecar car) (defun car (x) "mycar") (car 1) "mycar"; (setq car nil) (car 1) ; error: no function definition: CAR; (setq car savecar) (car '(1)) 1
            var lisp = new LispInterpreter();
            lisp.Run("(setq savecar car)");
            lisp.Run("(defun car (x) \"mycar\")");

            Expect(lisp, "(car 1)", "\"mycar\"");
            Expect(lisp, "(setq car nil)", "nil");
            Expect(lisp, "(car 1)", "; error: no function definition: CAR");
            lisp.Run("(setq car savecar)");
            Expect(lisp, "(car '(1))", "1");
        }

        [Fact]
        public void TheSymbolTIsAVariable_LikeAnyOther()
        {
            // AutoCAD 2021: (setq t 5) 5   !t 5   (setq t (quote T)) T
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq t 5)", "5");
            Expect(lisp, "t", "5");
            Expect(lisp, "(setq t (quote T))", "T");
            Expect(lisp, "t", "T");
        }

        [Fact]
        public void Setq_SetsEachPairInTurn_AndGivesTheLastValue()
        {
            // AutoCAD 2021: (setq lst (list 1 2 3) lst2 (cdr lst)) (2 3)
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq lst (list 1 2 3) lst2 (cdr lst))", "(2 3)");
            Expect(lisp, "lst", "(1 2 3)");
        }

        [Fact]
        public void SetValueAndGetValue_ReachTheValueCellOfAnyCase()
        {
            var lisp = new LispInterpreter();
            lisp.SetValue("Seven", 7);

            Assert.Equal(7, lisp.GetValue("SEVEN"));
            Assert.Equal("14", lisp.Run("(* seven 2)").Output);
            Assert.Null(lisp.GetValue("nosuchvariable"));
        }

        #endregion

        #region wcmatch

        [Theory]
        [InlineData("abcde", "a*e", "T")] // AutoLISP Reference (not measured): * is any run of characters
        [InlineData("abc", "a?c", "T")] // AutoLISP Reference: ? is any one character
        [InlineData("a1c", "a#c", "T")] // AutoLISP Reference: # is one digit
        [InlineData("abc", "a#c", "nil")] // AutoLISP Reference: b is not a digit
        [InlineData("abc", "@@@", "T")] // AutoLISP Reference: @ is one letter
        [InlineData("ab1", "@@@", "nil")] // AutoLISP Reference: 1 is not a letter
        [InlineData("a-c", "a.c", "T")] // AutoLISP Reference: . is one character that is neither letter nor digit
        [InlineData("abc", "a.c", "nil")] // AutoLISP Reference: b is a letter
        [InlineData("abc", "[a-c]bc", "T")] // AutoLISP Reference: [a-c] is one of a, b, c
        [InlineData("dbc", "[a-c]bc", "nil")] // AutoLISP Reference: d is not in [a-c]
        [InlineData("dbc", "[~a-c]bc", "T")] // AutoLISP Reference: [~a-c] is one that is not in the range
        [InlineData("abc", "[xa]bc", "T")] // AutoLISP Reference: [xa] is x or a
        [InlineData("abc", "~a*", "nil")] // AutoLISP Reference: a ~ at the start is anything but the pattern
        [InlineData("xbc", "~a*", "T")] // AutoLISP Reference: xbc does not match a*
        [InlineData("abc", "x*,a*", "T")] // AutoLISP Reference: a comma separates patterns, any one may match
        [InlineData("abc", "x*,y*", "nil")] // AutoLISP Reference: none of them matches
        [InlineData("abc", "ABC", "nil")] // the comparison is case-sensitive
        [InlineData("a*c", "a`*c", "T")] // AutoLISP Reference: a reverse quote makes the next character itself
        [InlineData("abc", "a`*c", "nil")] // AutoLISP Reference: the * is then a letter of its own
        [InlineData("", "*", "T")] // * matches nothing too
        [InlineData("abc", "", "nil")] // an empty pattern matches only the empty text
        [InlineData("abc", "*b*", "T")] // * on both sides
        [InlineData("abcabc", "*c", "T")] // * backs off to the last c
        [InlineData("abc", "abc*", "T")] // * at the end matches nothing
        [InlineData("ab", "abc", "nil")] // the text is shorter than the pattern
        public void Wcmatch_FollowsTheWildcardRulesOfAutoLisp(string text, string pattern, string output)
        {
            string expression = "(wcmatch \"" + text + "\" \"" + pattern + "\")";

            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void Wcmatch_RefusesWhatIsNotAString()
        {
            Assert.Equal("; error: bad argument type: stringp 1", Out("(wcmatch 1 \"a\")"));
            Assert.Equal("; error: bad argument type: stringp nil", Out("(wcmatch \"a\" nil)"));
        }

        #endregion

        #region Later stages

        [Theory]
        [InlineData("(alert \"x\")", "; error: no function definition: ALERT")] // L2
        [InlineData("(grtext)", "; error: no function definition: GRTEXT")] // L2
        [InlineData("(redraw)", "; error: no function definition: REDRAW")] // L2
        public void AFunctionOfL2ThatNeedsADrawing_IsNotDefinedInABareInterpreter(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-get-acad-object)", "; error: no function definition: VLAX-GET-ACAD-OBJECT")] // AutoCAD 2021: ; error: no function definition: VLAX-GET-ACAD-OBJECT
        [InlineData("(getvar \"LUNITS\")", "; error: no function definition: GETVAR")] // a function of a later stage is not defined yet
        [InlineData("(ssget)", "; error: no function definition: SSGET")] // a function of a later stage is not defined yet
        public void AFunctionOfALaterStage_IsNotDefined(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        #endregion

        #region Speed

        [Fact]
        public void AMillionSetqsInARepeat_TakeUnderTwoSeconds()
        {
            // AutoCAD 2021: (repeat 1000000 (setq i (1+ i))) 157 ms, the while form 125 ms.
            string result = null;

            TimeSpan took = SpeedGuard.AssertWithin(
                2.0,
                () =>
                {
                    var lisp = new LispInterpreter();
                    lisp.Run("(setq i 0)");
                    return lisp;
                },
                lisp => result = lisp.Run("(repeat 1000000 (setq i (1+ i)))").Output
            );

            Assert.Equal("1000000", result);
            _output.WriteLine("repeat: " + took.TotalMilliseconds.ToString("0") + " ms (AutoCAD 2021: 157 ms)");
        }

        [Fact]
        public void AMillionSetqsInAWhile_TakeUnderTwoSeconds()
        {
            // AutoCAD 2021: (while (< i 1000000) (setq i (1+ i))) 125 ms.
            string result = null;

            TimeSpan took = SpeedGuard.AssertWithin(
                2.0,
                () =>
                {
                    var lisp = new LispInterpreter();
                    lisp.Run("(setq i 0)");
                    return lisp;
                },
                lisp => result = lisp.Run("(while (< i 1000000) (setq i (1+ i)))").Output
            );

            Assert.Equal("1000000", result);
            _output.WriteLine("while: " + took.TotalMilliseconds.ToString("0") + " ms (AutoCAD 2021: 125 ms)");
        }

        #endregion
    }
}
