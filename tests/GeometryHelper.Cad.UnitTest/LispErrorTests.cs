using System.Linq;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Errors, <c>*error*</c>, <c>vl-catch-all-apply</c>, the hard error of the stack limit, and <c>c:</c> commands, against
    /// AutoCAD 2021's accoreconsole (probes p1 to p4). Each row is an expression and the line AutoCAD wrote for it; the
    /// messages are AutoCAD's own, including the colon after <c>numberp</c> and none after <c>consp</c>.
    /// </summary>
    [Collection("LispSerial")]
    public class LispErrorTests
    {
        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        private static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        private const string Hard = "Hard error occurred ***\ninternal stack limit reached (simulated)";

        [Theory]
        [InlineData("(+ 1 \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(car 1)", "bad argument type: consp 1")] // AutoCAD 2021: ; error: bad argument type: consp 1
        [InlineData("(foo)", "no function definition: FOO")] // AutoCAD 2021: ; error: no function definition: FOO
        [InlineData("(foo 1 2)", "no function definition: FOO")] // AutoCAD 2021: ; error: no function definition: FOO
        [InlineData("(setq)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(setq a)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(setq a 1 b)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(car)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(car '(1) '(2))", "too many arguments")] // AutoCAD 2021: ; error: too many arguments
        [InlineData("(/ 1 0)", "divide by zero")] // AutoCAD 2021: ; error: divide by zero
        [InlineData("(/ 1.0 0)", "divide by zero")] // AutoCAD 2021: ; error: divide by zero
        [InlineData("(/ 1 0.0)", "divide by zero")] // AutoCAD 2021: ; error: divide by zero
        [InlineData("(nth -1 '(1))", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1
        [InlineData("(substr \"abc\" 0)", "bad argument value: positive 0")] // AutoCAD 2021: ; error: bad argument value: positive 0
        [InlineData("(substr \"hello\" 0 1)", "bad argument value: positive 0")] // AutoCAD 2021: ; error: bad argument value: positive 0
        [InlineData("(substr \"hello\" 2 -1)", "bad argument value: non-negative: -1")] // AutoCAD 2021: ; error: bad argument value: non-negative: -1
        [InlineData("(strcat 1)", "bad argument type: stringp 1")] // AutoCAD 2021: ; error: bad argument type: stringp 1
        [InlineData("(strcat \"a\" 'b)", "bad argument type: stringp B")] // AutoCAD 2021: ; error: bad argument type: stringp B
        [InlineData("(strcat \"a\" nil)", "bad argument type: stringp nil")] // AutoCAD 2021: ; error: bad argument type: stringp nil
        [InlineData("(sqrt -1)", "function undefined for argument: -1")] // AutoCAD 2021: ; error: function undefined for argument: -1
        [InlineData("(sqrt -1.0)", "function undefined for argument: -1.0")] // AutoCAD 2021: ; error: function undefined for argument: -1.0
        [InlineData("(log 0)", "function undefined for argument: 0")] // AutoCAD 2021: ; error: function undefined for argument: 0
        [InlineData("(log -1)", "function undefined for argument: -1")] // AutoCAD 2021: ; error: function undefined for argument: -1
        [InlineData("(rem 7 0)", "divide by zero")] // AutoCAD 2021: ; error: divide by zero
        [InlineData("(gcd -12 18)", "improper argument: -12")] // AutoCAD 2021: ; error: improper argument: -12
        [InlineData("(gcd 0 5)", "improper argument: 0")] // AutoCAD 2021: ; error: improper argument: 0
        [InlineData("(gcd 5)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(itoa 3.0)", "bad argument type: fixnump: 3.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 3.0
        [InlineData("(< 1 \"a\")", "bad argument type for compare: 1 \"a\"")] // AutoCAD 2021: ; error: bad argument type for compare: 1 "a"
        [InlineData("(< 'a 'b)", "bad argument type for compare: A B")] // AutoCAD 2021: ; error: bad argument type for compare: A B
        [InlineData("(append '(1) 2)", "bad argument type: listp 2")] // AutoCAD 2021: ; error: bad argument type: listp 2
        [InlineData("(length '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2
        [InlineData("(cdr \"a\")", "bad argument type: consp \"a\"")] // AutoCAD 2021: ; error: bad argument type: consp "a"
        [InlineData("(read \"(1\")", "malformed list on input")] // AutoCAD 2021: ; error: malformed list on input
        [InlineData("(ff)", "no function definition: FF")] // an undefined function, with no arguments
        [InlineData("(if)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(if t 1 2 3)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(quote a b)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(quote)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(set \"q\" 5)", "bad argument type: symbolp \"q\"")] // AutoCAD 2021: ; error: bad argument type: symbolp "q"
        [InlineData("(setq 1 2)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(repeat 2.0 1)", "bad argument type: fixnump: 2.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 2.0
        [InlineData("(repeat \"a\" 1)", "bad argument type: fixnump: \"a\"")] // AutoCAD 2021: ; error: bad argument type: fixnump: "a"
        [InlineData("(repeat 1.5 1)", "bad argument type: fixnump: 1.5")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.5
        [InlineData("(repeat 99999999999 1)", "bad argument type: fixnump: 1.0e+11")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.0e+11
        [InlineData("(cond (nil 1) 5)", "syntax error")] // a clause that is not a list
        [InlineData("(cond 5)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(cond ((1 2)))", "bad function: 1")] // AutoCAD 2021: ; error: bad function: 1
        [InlineData("(foreach)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(foreach x)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(foreach \"x\" '(1) 1)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(foreach x '(1 . 2) x)", "bad argument type: consp 2")] // AutoCAD 2021: ; error: bad argument type: consp 2
        [InlineData("(foreach x 5 x)", "bad argument type: consp 5")] // AutoCAD 2021: ; error: bad argument type: consp 5
        [InlineData("(lambda)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(lambda x)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(setq x 1 y 2 z)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(setq 'x 1)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(setq nil 5)", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(set 'x)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(apply 'setq '(zz 1))", "syntax error")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(mapcar 'nosuch '(1))", "no function definition: nil")] // AutoCAD 2021: ; error: no function definition: nil
        [InlineData("(apply 'nosuch '(1))", "no function definition: nil")] // AutoCAD 2021: ; error: no function definition: nil
        [InlineData("(eval '(nosuch))", "no function definition: NOSUCH")] // AutoCAD 2021: ; error: no function definition: NOSUCH
        [InlineData("(nth 1.0 '(1 2))", "bad argument type: fixnump: 1.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.0
        [InlineData("(nth 1 '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2
        [InlineData("(nth 0 nil)", "bad argument type: consp nil")] // AutoCAD 2021: ; error: bad argument type: consp nil
        [InlineData("(nth 0 \"a\")", "bad argument type: consp \"a\"")] // AutoCAD 2021: ; error: bad argument type: consp "a"
        [InlineData("(assoc 1 '(1 2))", "bad association list: (1 2)")] // AutoCAD 2021: ; error: bad association list: (1 2)
        [InlineData("(member 2 '(1 . 2))", "bad argument type: consp 2")] // AutoCAD 2021: ; error: bad argument type: consp 2
        [InlineData("(reverse '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2
        [InlineData("(last '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2
        [InlineData("(subst 1 2 '(2 . 2))", "bad argument type: consp 2")] // AutoCAD 2021: ; error: bad argument type: consp 2
        [InlineData("(mapcar '1+ '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2
        [InlineData("(1+ \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(min 1 \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(fix \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(float \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(zerop \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(minusp \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(expt 0 0)", "function undefined for 0 in negative power")] // AutoCAD 2021: ; error: function undefined for 0 in negative power
        [InlineData("(expt 0 -1)", "function undefined for 0 in negative power")] // AutoCAD 2021: ; error: function undefined for 0 in negative power
        [InlineData("(expt -8 (/ 1.0 3))", "x**y is undefined when x is real < 0, and y is not a whole number")] // AutoCAD 2021: ; error: x**y is undefined when x is real < 0, and y is not a whole number
        [InlineData("(lsh 1.0 1)", "bad argument type: INT 1.0")] // AutoCAD 2021: ; error: bad argument type: INT 1.0
        [InlineData("(logand 1.0)", "bad argument type: fixnump: 1.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 1.0
        [InlineData("(~ 1.0)", "bad argument type: INT 1.0")] // AutoCAD 2021: ; error: bad argument type: INT 1.0
        [InlineData("(equal 1 2 \"a\")", "bad argument type: numberp: \"a\"")] // AutoCAD 2021: ; error: bad argument type: numberp: "a"
        [InlineData("(=)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(<)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(strcase 1)", "bad argument type: stringp 1")] // AutoCAD 2021: ; error: bad argument type: stringp 1
        [InlineData("(substr \"hello\" 2.0)", "bad argument type: fixnump: 2.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 2.0
        [InlineData("(chr 65.0)", "bad argument type: fixnump: 65.0")] // AutoCAD 2021: ; error: bad argument type: fixnump: 65.0
        [InlineData("(atoi 5)", "bad argument type: stringp 5")] // AutoCAD 2021: ; error: bad argument type: stringp 5
        [InlineData("(princ \"x\" nil)", "bad argument type: FILE nil")] // AutoCAD 2021: ; error: bad argument type: FILE nil
        [InlineData("(princ \"a\" \"b\")", "bad argument type: FILE \"b\"")] // AutoCAD 2021: ; error: bad argument type: FILE "b"
        [InlineData("(prompt 1)", "bad argument type: stringp 1")] // AutoCAD 2021: ; error: bad argument type: stringp 1
        [InlineData("(terpri 1)", "too many arguments")] // AutoCAD 2021: ; error: too many arguments
        [InlineData("(atoms-family 1 '(car))", "invalid argument supplied, must be a string. CAR")] // AutoCAD 2021: ; error: invalid argument supplied, must be a string. CAR
        [InlineData("(not)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(not 1 2)", "too many arguments")] // AutoCAD 2021: ; error: too many arguments
        [InlineData("(boundp 'a 'b)", "too many arguments")] // AutoCAD 2021: ; error: too many arguments
        [InlineData("(cons)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(cons 1)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(cons 1 2 3)", "too many arguments")] // AutoCAD 2021: ; error: too many arguments
        [InlineData("(length \"a\")", "bad argument type: listp \"a\"")] // AutoCAD 2021: ; error: bad argument type: listp "a"
        [InlineData("(foo (car 1))", "bad argument type: consp 1")] // AutoCAD 2021: ; error: bad argument type: consp 1 (the arguments are evaluated first)
        [InlineData("(car (foo))", "no function definition: FOO")] // AutoCAD 2021: ; error: no function definition: FOO
        [InlineData("(vl-catch-all-error-message 5)", "argument is not a vl-catch-all-apply error: 5")] // AutoCAD 2021: ; error: argument is not a vl-catch-all-apply error: 5
        [InlineData("(vl-catch-all-error-message (vl-catch-all-apply 'nosuch nil))", "bad function: NOSUCH")] // AutoCAD 2021: ; error: bad function: NOSUCH
        [InlineData("(vl-catch-all-apply 'foo nil)", "bad function: FOO")] // AutoCAD 2021: ; error: bad function: FOO
        [InlineData("(load 5)", "bad argument type: stringp 5")] // AutoCAD 2021: ; error: bad argument type: stringp 5
        [InlineData("(load)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments
        [InlineData("(exit)", "quit / exit abort")] // AutoCAD 2021: ; error: quit / exit abort
        [InlineData("(quit)", "quit / exit abort")] // AutoCAD 2021: ; error: quit / exit abort
        [InlineData("(*error* \"x\")", "no function definition: *ERROR*")] // AutoCAD 2021: ; error: no function definition: *ERROR*
        public void AnError_IsWrittenAsAutoCadWritesIt(string expression, string message)
        {
            LispResult result = new LispInterpreter().Run(expression);

            Assert.Equal("; error: " + message, result.Output);
            Assert.True(result.Failed);
            Assert.Equal(message, result.Error);
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData("(defun f5 (a a) a)", "(f5 1 2)", "no function definition: F5")] // AutoCAD 2021: ; error: syntax error, then ; error: no function definition: F5
        [InlineData("(defun f6 (\"a\") 1)", "", "")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(defun f7 x 1)", "", "")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(defun \"f8\" () 1)", "", "")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(defun f9)", "", "")] // AutoCAD 2021: ; error: syntax error
        [InlineData("(defun f10 ())", "(f10)", "no function definition: F10")] // AutoCAD 2021: ; error: syntax error, then ; error: no function definition: F10
        public void ABadDefun_IsASyntaxError_AndDefinesNothing(string definition, string call, string callMessage)
        {
            var lisp = new LispInterpreter();

            Assert.Equal("; error: syntax error", lisp.Run(definition).Output);

            if (call.Length > 0)
            {
                Assert.Equal("; error: " + callMessage, lisp.Run(call).Output);
            }
        }

        [Fact]
        public void BadFunctionNamesTheValueThatWasCalled()
        {
            // AutoCAD 2021: ((quote car) '(1)) bad function: CAR   ('car '(1)) bad function: CAR   (1 2) bad function: 1
            // ("car" '(1)) bad function: "car"   (nil) no function definition: nil   ((lambda (x) x)) too few arguments
            // (setq nf 5) (nf) bad function: 5   (eval '(1 2)) bad function: 1   (eval '("a")) bad function: "a"
            var lisp = new LispInterpreter();

            Expect(lisp, "((quote car) '(1))", "; error: bad function: CAR");
            Expect(lisp, "('car '(1))", "; error: bad function: CAR");
            Expect(lisp, "(1 2)", "; error: bad function: 1");
            Expect(lisp, "(\"car\" '(1))", "; error: bad function: \"car\"");
            Expect(lisp, "(nil)", "; error: no function definition: nil");
            Expect(lisp, "((lambda (x) x))", "; error: too few arguments");
            Expect(lisp, "(setq nf 5)", "5");
            Expect(lisp, "(nf)", "; error: bad function: 5");
            Expect(lisp, "(eval '(1 2))", "; error: bad function: 1");
            Expect(lisp, "(eval '(\"a\"))", "; error: bad function: \"a\"");
        }

        [Fact]
        public void AUserFunctionWithTheWrongNumberOfArguments_IsAnError()
        {
            // AutoCAD 2021: (defun ff (a / b) (setq b a) b) (ff) too few arguments, (ff 1 2) too many arguments
            var lisp = new LispInterpreter();
            lisp.Run("(defun ff (a / b) (setq b a) b)");

            Expect(lisp, "(ff)", "; error: too few arguments");
            Expect(lisp, "(ff 1 2)", "; error: too many arguments");
        }

        [Fact]
        public void AnErrorInAUserFunction_IsTheErrorOfTheFunctionItCalled()
        {
            // AutoCAD 2021: (defun gg () (+ 1 "a")) (gg) ; error: bad argument type: numberp: "a"
            var lisp = new LispInterpreter();
            lisp.Run("(defun gg () (+ 1 \"a\"))");

            Expect(lisp, "(gg)", "; error: bad argument type: numberp: \"a\"");
        }

        [Fact]
        public void AnErrorAfterSomeOutput_IsWrittenWhereTheOutputStopped()
        {
            // AutoCAD 2021: (progn (princ "x") (car 1)) x; error: bad argument type: consp 1   with "x\n" the error is on the next line
            Expect(new LispInterpreter(), "(progn (princ \"x\") (car 1))", "x; error: bad argument type: consp 1");
            Expect(new LispInterpreter(), "(progn (princ \"x\\n\") (car 1))", "x\n; error: bad argument type: consp 1");
        }

        #region *error*

        [Fact]
        public void ALambdaAsStarErrorGetsTheMessage_AndTheErrorLineIsReplacedByWhatItWrites()
        {
            // AutoCAD 2021: (setq *error* (lambda (m) (princ (strcat "ERR[" m "]")) (princ))) then (+ 1 "a") ERR[bad argument type: numberp: "a"]
            // (exit) ERR[quit / exit abort]   (quit) ERR[quit / exit abort]   (foo) ERR[no function definition: FOO]
            var lisp = new LispInterpreter();

            Assert.Matches(
                "^#<SUBR @[0-9a-f]{16} -lambda->$",
                lisp.Run("(setq *error* (lambda (m) (princ (strcat \"ERR[\" m \"]\")) (princ)))").Output
            );
            Expect(lisp, "(+ 1 \"a\")", "ERR[bad argument type: numberp: \"a\"]");
            Expect(lisp, "(exit)", "ERR[quit / exit abort]");
            Expect(lisp, "(quit)", "ERR[quit / exit abort]");
            Expect(lisp, "(foo)", "ERR[no function definition: FOO]");
            Expect(lisp, "(setq *error* nil)", "nil");
            Expect(lisp, "(foo)", "; error: no function definition: FOO");
        }

        [Fact]
        public void TheResultOfARunThatEndedInAnError_IsFailedEvenWhenStarErrorHandledIt()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(defun *error* (m) (princ \"handled\"))");
            LispResult result = lisp.Run("(car 1)");

            Assert.Equal("handled", result.Output);
            Assert.True(result.Failed);
            Assert.Equal("bad argument type: consp 1", result.Error);
        }

        [Fact]
        public void StarErrorSeesTheLocalsOfTheFunctionThatFailed_AndTheyAreGivenBackAfterIt()
        {
            // AutoCAD 2021: (setq a 1) (defun h (/ a) (setq a 2) (car 1)) (h) ; error: bad argument type: consp 1   !a 1
            // (defun h2 (a) (setq a 9) (car 1)) (h2 5) ; error: ...   !a 1
            // (defun *error* (m) (princ (strcat "\nE:" m)) (princ "|a=") (princ a) (princ)) (h) E:bad argument type: consp 1|a=2
            var lisp = new LispInterpreter();
            lisp.Run("(setq a 1)");
            lisp.Run("(defun h (/ a) (setq a 2) (car 1))");
            Expect(lisp, "(h)", "; error: bad argument type: consp 1");
            Assert.Equal(1, lisp.GetValue("a"));
            lisp.Run("(defun h2 (a) (setq a 9) (car 1))");
            Expect(lisp, "(h2 5)", "; error: bad argument type: consp 1");
            Assert.Equal(1, lisp.GetValue("a"));

            Expect(
                lisp,
                "(defun *error* (m) (princ (strcat \"\\nE:\" m)) (princ \"|a=\") (princ a) (princ))",
                "*ERROR*"
            );
            Expect(lisp, "(h)", "\nE:bad argument type: consp 1|a=2");
            Assert.Equal(1, lisp.GetValue("a"));
            Expect(lisp, "(h2 5)", "\nE:bad argument type: consp 1|a=9");
            Assert.Equal(1, lisp.GetValue("a"));
        }

        [Fact]
        public void AnErrorInsideStarError_PrintsItsMessageAlone()
        {
            // AutoCAD 2021: (defun *error* (m) (car 1)) (car 1) bad argument type: consp 1   (defun *error* (m) (exit)) (car 1) quit / exit abort
            // (defun *error* (m) (princ (strcat "\n[" m "]")) 7) (car 1) [bad argument type: consp 1]
            var lisp = new LispInterpreter();

            lisp.Run("(defun *error* (m) (princ (strcat \"\\n[\" m \"]\")) 7)");
            Expect(lisp, "(car 1)", "\n[bad argument type: consp 1]");
            lisp.Run("(defun *error* (m) (car 1))");
            Expect(lisp, "(car 1)", "bad argument type: consp 1");
            lisp.Run("(defun *error* (m) (exit))");
            Expect(lisp, "(car 1)", "quit / exit abort");
        }

        [Fact]
        public void AStarErrorThatIsNotAFunction_IsBadFunctionWithNoErrorPrefix()
        {
            // AutoCAD 2021: (setq *error* 5) (car 1) bad function: 5
            var lisp = new LispInterpreter();
            lisp.Run("(setq *error* 5)");

            Expect(lisp, "(car 1)", "bad function: 5");
        }

        [Fact]
        public void AStarErrorDefinedInsideACommand_IsCalledForAnErrorOfThatCommand()
        {
            // AutoCAD 2021: (defun c:ghe () (defun *error* (m) (princ "cmd-err:") (princ m) (princ)) (car 1)) GHE cmd-err:bad argument type: consp 1
            var lisp = new LispInterpreter();
            lisp.Run("(defun c:ghe () (defun *error* (m) (princ \"cmd-err:\") (princ m) (princ)) (car 1))");

            Assert.Equal("cmd-err:bad argument type: consp 1", lisp.RunCommand("GHE").Output);
        }

        [Fact]
        public void TheNestedFramesOfAFailedCall_AreGivenBackInnermostFirst()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq v 'top)");
            lisp.Run("(defun outer (v) (inner 2))");
            lisp.Run("(defun inner (v) (car 1))");

            Expect(lisp, "(outer 1)", "; error: bad argument type: consp 1");
            Assert.Equal("TOP", LispInterpreter.Print(lisp.GetValue("v"), false));
        }

        #endregion

        #region vl-catch-all-apply

        [Fact]
        public void CatchAllApply_GivesTheValue_OrTheErrorAsAValue()
        {
            // AutoCAD 2021: (vl-catch-all-apply '+ '(1 2)) 3   (vl-catch-all-apply '/ '(1 0)) #<%catch-all-apply-error%>
            // (vl-catch-all-error-message ...) "divide by zero"   (vl-catch-all-error-p ...) T   (vl-catch-all-apply 'car '((1 2))) 1
            var lisp = new LispInterpreter();

            Expect(lisp, "(vl-catch-all-apply '+ '(1 2))", "3");
            Expect(lisp, "(vl-catch-all-apply '/ '(1 0))", "#<%catch-all-apply-error%>");
            Expect(lisp, "(vl-catch-all-error-message (vl-catch-all-apply '/ '(1 0)))", "\"divide by zero\"");
            Expect(lisp, "(vl-catch-all-error-p (vl-catch-all-apply '/ '(1 0)))", "T");
            Expect(lisp, "(vl-catch-all-error-p 5)", "nil");
            Expect(lisp, "(vl-catch-all-apply 'car '((1 2)))", "1");
        }

        [Theory]
        [InlineData("(vl-catch-all-error-message (vl-catch-all-apply 'exit nil))", "\"quit / exit abort\"")] // AutoCAD 2021: "quit / exit abort"
        [InlineData("(vl-catch-all-error-message (vl-catch-all-apply 'car '(1)))", "\"bad argument type: consp 1\"")] // AutoCAD 2021: "bad argument type: consp 1"
        [InlineData("(vl-catch-all-error-message (vl-catch-all-apply 'car nil))", "\"too few arguments\"")] // AutoCAD 2021: "too few arguments"
        [InlineData("(vl-catch-all-error-message (vl-catch-all-apply 'car '(1 2)))", "\"too many arguments\"")] // AutoCAD 2021: "too many arguments"
        [InlineData(
            "(vl-catch-all-error-message (vl-catch-all-apply '(lambda () (exit)) nil))",
            "\"quit / exit abort\""
        )] // AutoCAD 2021: "quit / exit abort"
        [InlineData(
            "(vl-catch-all-error-message (vl-catch-all-apply '(lambda () (quit)) nil))",
            "\"quit / exit abort\""
        )] // AutoCAD 2021: "quit / exit abort"
        [InlineData(
            "(vl-catch-all-error-message (vl-catch-all-apply '(lambda () (+ 1 \"a\")) nil))",
            "\"bad argument type: numberp: \\\"a\\\"\""
        )] // AutoCAD 2021: "bad argument type: numberp: \"a\""
        [InlineData("(vl-catch-all-apply '+ 5)", "")] // AutoCAD 2021: nothing is echoed
        public void CatchAllApply_TakesTheMessageOfTheError(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void CatchAllApply_GivesBackTheVariablesTheFailedCallBound()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq a 'outside)");
            lisp.Run("(defun fail (a) (car 1))");

            Expect(lisp, "(vl-catch-all-error-p (vl-catch-all-apply 'fail '(1)))", "T");
            Assert.Equal("OUTSIDE", LispInterpreter.Print(lisp.GetValue("a"), false));
        }

        [Fact]
        public void StarError_IsNotCalledForAnErrorThatCatchAllApplyCaught()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(defun *error* (m) (princ \"called\"))");

            Expect(lisp, "(vl-catch-all-error-p (vl-catch-all-apply 'car '(1)))", "T");
        }

        #endregion

        #region The hard error

        [Fact]
        public void ARecursionOfSeventeenThousandFiveHundred_Works()
        {
            // AutoCAD 2021: (defun ghr (n) (if (> n 0) (ghr (1- n)) 'done)) (ghr 12000) DONE (ghr 15000) DONE (ghr 17500) DONE
            var lisp = new LispInterpreter();

            Expect(lisp, "(defun ghr (n) (if (> n 0) (ghr (1- n)) 'done))", "GHR");
            Expect(lisp, "(ghr 12000)", "DONE");
            Expect(lisp, "(ghr 15000)", "DONE");
            Expect(lisp, "(ghr 17500)", "DONE");
        }

        [Fact]
        public void ALocalsAndFunctionRecursion_Works()
        {
            // AutoCAD 2021: (defun ghr2 (n / a b c) (setq a 1 b 2 c 3) (if (> n 0) (progn (ghr2 (1- n))) 'done)) (ghr2 15000) DONE
            var lisp = new LispInterpreter();
            lisp.Run("(defun ghr2 (n / a b c) (setq a 1 b 2 c 3) (if (> n 0) (progn (ghr2 (1- n))) 'done))");

            Expect(lisp, "(ghr2 12000)", "DONE");
            Expect(lisp, "(ghr2 15000)", "DONE");
        }

        [Theory]
        [InlineData(50000)] // AutoCAD 2021: Hard error occurred *** / internal stack limit reached (simulated)
        [InlineData(65000)] // AutoCAD 2021: the same
        [InlineData(80000)] // AutoCAD 2021: the same
        [InlineData(100000)] // AutoCAD 2021: the same
        public void ARecursionPastTheLimit_IsTheHardError(int depth)
        {
            var lisp = new LispInterpreter();
            lisp.Run("(defun ghr (n) (if (> n 0) (ghr (1- n)) 'done))");
            LispResult result = lisp.Run("(ghr " + depth + ")");

            Assert.Equal(Hard, result.Output);
            Assert.True(result.Failed);
            Assert.Equal("internal stack limit reached (simulated)", result.Error);
        }

        [Fact]
        public void AFunctionThatCallsItselfForever_IsTheHardError_AndTheInterpreterGoesOn()
        {
            // AutoCAD 2021: (defun ghinf () (ghinf)) (ghinf) Hard error ...   (princ "alive") alive"alive"
            var lisp = new LispInterpreter();
            lisp.Run("(defun ghinf () (ghinf))");

            Expect(lisp, "(ghinf)", Hard);
            Expect(lisp, "(princ \"alive\")", "alive\"alive\"");
        }

        [Fact]
        public void TheHardError_IsNotCaughtByCatchAllApply_AndStarErrorIsNotCalled()
        {
            // AutoCAD 2021: (vl-catch-all-apply 'ghr '(100000)) Hard error occurred ***, and (princ "alive") works after it
            var lisp = new LispInterpreter();
            lisp.Run("(defun ghr (n) (if (> n 0) (ghr (1- n)) 'done))");
            lisp.Run("(defun *error* (m) (princ \"called\"))");

            Expect(lisp, "(vl-catch-all-apply 'ghr '(100000))", Hard);
            Expect(lisp, "(princ \"alive\")", "alive\"alive\"");
        }

        [Fact]
        public void AfterTheHardError_EveryBindingIsGivenBack()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq n 7)");
            lisp.Run("(defun ghr (n / loc) (if (> n 0) (ghr (1- n)) 'done))");
            lisp.Run("(ghr 100000)");

            Assert.Equal(7, lisp.GetValue("n"));
            Assert.Null(lisp.GetValue("loc"));
        }

        [Fact]
        public void ADeeplyNestedListInTheText_IsTheHardError_NotACrash()
        {
            var lisp = new LispInterpreter();
            string text = new string('(', 2000000);

            Assert.True(lisp.Run(text).Failed);
            Expect(lisp, "(princ \"alive\")", "alive\"alive\"");
        }

        #endregion

        #region c: commands

        [Fact]
        public void ACommand_IsEchoedAsTheValueOfTheExpression()
        {
            // AutoCAD 2021: GHTST hi5   GHTST2 (nothing)   GHTST3 "str"   GHTST4 ; error: bad argument type: numberp: "a"
            // GHTST5 nil   GHTST6 ; error: quit / exit abort   (c:ghtst) hi5
            var lisp = new LispInterpreter();

            Expect(lisp, "(defun c:ghtst () (princ \"hi\") 5)", "C:GHTST");
            Assert.Equal("hi5", lisp.RunCommand("GHTST").Output);
            lisp.Run("(defun c:ghtst2 () (princ))");
            Assert.Equal("", lisp.RunCommand("GHTST2").Output);
            lisp.Run("(defun c:ghtst3 () \"str\")");
            Assert.Equal("\"str\"", lisp.RunCommand("GHTST3").Output);
            lisp.Run("(defun c:ghtst4 () (+ 1 \"a\"))");
            Assert.Equal("; error: bad argument type: numberp: \"a\"", lisp.RunCommand("GHTST4").Output);
            lisp.Run("(defun c:ghtst5 (/ a) (setq a 1) nil)");
            Assert.Equal("nil", lisp.RunCommand("GHTST5").Output);
            lisp.Run("(defun c:ghtst6 () (exit))");
            Assert.Equal("; error: quit / exit abort", lisp.RunCommand("GHTST6").Output);
            Expect(lisp, "(c:ghtst)", "hi5");
        }

        [Theory]
        [InlineData("(defun c:ghcmd () (setq ghv 1) 'sym)", "C:GHCMD", "GHCMD", "SYM")] // AutoCAD 2021: SYM
        [InlineData("(defun c:ghlist () (list 1 \"a\"))", "C:GHLIST", "GHLIST", "(1 \"a\")")] // AutoCAD 2021: (1 "a")
        [InlineData("(defun c:ghreal () 1.5)", "C:GHREAL", "GHREAL", "1.5")] // AutoCAD 2021: 1.5
        [InlineData("(defun c:ghpr () (princ \"x\"))", "C:GHPR", "GHPR", "x\"x\"")] // AutoCAD 2021: x"x"
        [InlineData("(defun c:ghnil () nil)", "C:GHNIL", "GHNIL", "nil")] // AutoCAD 2021: nil
        [InlineData("(defun c:ghstr () (prompt \"p\"))", "C:GHSTR", "GHSTR", "pnil")] // AutoCAD 2021: pnil
        [InlineData("(defun c:ghret () (setq a 'b))", "C:GHRET", "GHRET", "B")] // AutoCAD 2021: B
        [InlineData("(defun c:ghx (a) a)", "C:GHX", "GHX", "; error: too few arguments")] // AutoCAD 2021: ; error: too few arguments
        public void TheValueOfACommand_IsEchoedInPrin1Form(
            string definition,
            string defined,
            string command,
            string output
        )
        {
            var lisp = new LispInterpreter();

            Assert.Equal(defined, lisp.Run(definition).Output);
            Assert.Equal(output, lisp.RunCommand(command).Output);
        }

        [Fact]
        public void ACommandIsTypedInEitherCase()
        {
            // AutoCAD 2021: (defun c:GhMixed () (princ "mx") (princ)) GHMIXED mx   ghmixed mx
            var lisp = new LispInterpreter();
            lisp.Run("(defun c:GhMixed () (princ \"mx\") (princ))");

            Assert.Equal("mx", lisp.RunCommand("GHMIXED").Output);
            Assert.Equal("mx", lisp.RunCommand("ghmixed").Output);
            Assert.True(lisp.IsCommand("ghmixed"));
            Assert.True(lisp.IsCommand("GHMIXED"));
        }

        [Fact]
        public void OnlyADefunOfC_MakesACommand()
        {
            // AutoCAD 2021: (setq c:ghy (lambda () ...)) GHY Unknown command   (defun ghz () 1) GHZ Unknown command
            var lisp = new LispInterpreter();
            lisp.Run("(setq c:ghy (lambda () (princ \"y\") (princ)))");
            lisp.Run("(defun ghz () 1)");
            lisp.Run("(defun c:ghd () 1)");
            lisp.Run("(defun-q c:ghq () 1)");

            Assert.False(lisp.IsCommand("ghy"));
            Assert.False(lisp.IsCommand("ghz"));
            Assert.True(lisp.IsCommand("ghd"));
            Assert.True(lisp.IsCommand("ghq"));
            Assert.False(lisp.IsCommand("nosuch"));
        }

        [Fact]
        public void ACommandWhoseSymbolWasSetToNil_IsNoLongerACommand()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(defun c:ghd () 1)");
            lisp.Run("(setq c:ghd nil)");

            Assert.False(lisp.IsCommand("ghd"));
            Assert.DoesNotContain("GHD", lisp.CommandNames);
        }

        [Fact]
        public void TheCommandNames_AreSortedByOrdinalComparison_WithoutThePrefix()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(defun c:zeta () 1)");
            lisp.Run("(defun c:alpha () 1)");
            lisp.Run("(defun c:3d () 1)");
            lisp.Run("(defun c:Beta () 1)");
            lisp.Run("(defun other () 1)");

            Assert.Equal(new[] { "3D", "ALPHA", "BETA", "ZETA" }, lisp.CommandNames.ToArray());
        }

        [Fact]
        public void ACommandThatIsNotDefined_IsNoFunctionDefinition()
        {
            Assert.Equal(
                "; error: no function definition: C:NOSUCH",
                new LispInterpreter().RunCommand("nosuch").Output
            );
        }

        [Fact]
        public void TheCommandsOfOneInterpreter_AreNotThoseOfAnother()
        {
            // The interpreter is made once for each document: nothing one defines is seen by another (LISPINIT 1).
            var first = new LispInterpreter();
            var second = new LispInterpreter();
            first.Run("(defun c:mine () 1)");

            Assert.True(first.IsCommand("mine"));
            Assert.False(second.IsCommand("mine"));
            Assert.Empty(second.CommandNames);
        }

        #endregion
    }
}
