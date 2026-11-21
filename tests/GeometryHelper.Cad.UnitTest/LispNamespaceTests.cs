using System;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The blackboard, <c>vl-propagate</c>, <c>vl-load-all</c>, <c>vl-doc-*</c>, the VLX functions and the refusals of
    /// AutoCAD-only functions, against AutoCAD 2021's accoreconsole (probe r4, L3). Two interpreters stand for two documents:
    /// the blackboard is shared at once, what <c>vl-propagate</c> and <c>vl-load-all</c> hand over reaches an interpreter at
    /// its next evaluation, and one made later at its first. Each test uses names of its own and clears the shared state when
    /// it is done.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispNamespaceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-namespace-" + Guid.NewGuid().ToString("N")
        );

        public LispNamespaceTests()
        {
            LispApplication.Reset();
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            LispApplication.Reset();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        private string File(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            System.IO.File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path.Replace('\\', '/');
        }

        [Fact]
        public void TheBlackboard_IsSeenAtOnceByAnotherInterpreter_AndIsNotASymbolOfEither()
        {
            // AutoCAD 2021 (r4): (vl-bb-set 'ghbb 5) 5, (vl-bb-ref 'ghbb) 5, (vl-bb-ref 'nosuch) nil, (boundp 'ghbb) nil
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            Expect(one, "(vl-bb-set 'ghnsbb 5)", "5");
            Expect(one, "(vl-bb-ref 'ghnsbb)", "5");
            Expect(two, "(vl-bb-ref 'ghnsbb)", "5");
            Expect(two, "(vl-bb-ref 'ghnsnosuch)", "nil");
            Expect(one, "(boundp 'ghnsbb)", "nil");
            Expect(two, "(boundp 'ghnsbb)", "nil");
        }

        [Fact]
        public void ABlackboardValue_IsReplacedByTheNextSet_AndAListKeepsItsForm()
        {
            // AutoCAD 2021 (r4): (vl-bb-set 'ghbb '(1 "a" b)) (1 "a" B), (vl-bb-ref 'ghbb) (1 "a" B)
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            Expect(one, "(vl-bb-set 'ghnsbb2 5)", "5");
            Expect(one, "(vl-bb-set 'ghnsbb2 '(1 \"a\" b))", "(1 \"a\" B)");
            Expect(two, "(vl-bb-ref 'ghnsbb2)", "(1 \"a\" B)");
        }

        [Fact]
        public void ASymbolAndAStringOfTheSameName_AreOneNameOnTheBlackboard()
        {
            // AutoCAD 2021 (r4): (vl-bb-set "x" 1) 1, (vl-bb-ref "x") 1 (ours: the string stands for the symbol of that name)
            var lisp = new LispInterpreter();

            Expect(lisp, "(vl-bb-set \"ghnsx\" 1)", "1");
            Expect(lisp, "(vl-bb-ref \"ghnsx\")", "1");
            Expect(lisp, "(vl-bb-ref 'ghnsx)", "1");
            Expect(lisp, "(vl-bb-ref 5)", "; error: bad argument type: symbolp 5");
        }

        [Fact]
        public void AValueThatCrosses_IsACopyInTheOtherInterpreter_WithSymbolsInternedByName()
        {
            // ours: D118, the copy as the receiving interpreter holds it
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            Expect(one, "(vl-bb-set 'ghnsl '(foo (bar \"s\" 1.5) . 3))", "(FOO (BAR \"s\" 1.5) . 3)");
            Expect(two, "(vl-bb-ref 'ghnsl)", "(FOO (BAR \"s\" 1.5) . 3)");
            Expect(two, "(eq (car (vl-bb-ref 'ghnsl)) 'foo)", "T");
            Expect(two, "(setq foo 9)", "9");
            Expect(two, "(eval (car (vl-bb-ref 'ghnsl)))", "9");
        }

        [Fact]
        public void AFunctionWrittenInLisp_IsRebuiltInTheOtherInterpreter_AndSeesItsVariables()
        {
            // ours: D118, a user function is rebuilt from its parameter list and body, so its free variables are the receiver's
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            one.Run("(setq ghnsk 1)");
            two.Run("(setq ghnsk 10)");
            one.Run("(vl-bb-set 'ghnsf (lambda (x / y) (setq y (* x 2)) (+ y ghnsk)))");

            Expect(one, "(apply (vl-bb-ref 'ghnsf) '(4))", "9");
            Expect(two, "(apply (vl-bb-ref 'ghnsf) '(4))", "18");
            Expect(two, "(type (vl-bb-ref 'ghnsf))", "SUBR");
            Expect(two, "(boundp 'y)", "nil");
        }

        [Fact]
        public void ABuiltInOnTheBlackboard_IsTheOtherInterpretersOwnOfThatName()
        {
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            one.Run("(vl-bb-set 'ghnsb car)");

            Expect(two, "(apply (vl-bb-ref 'ghnsb) '((7 8)))", "7");
        }

        [Fact]
        public void VlPropagate_ReachesAnOpenInterpreterAtItsNextEvaluation_AndOneMadeLater()
        {
            // AutoCAD 2021 (r4): (setq ghd 7) ... (vl-propagate 'ghd) 8, the value; (vl-propagate 'nosuchvar) nil
            var one = new LispInterpreter();
            var open = new LispInterpreter();
            Expect(open, "(boundp 'ghnsp)", "nil");

            Expect(one, "(setq ghnsp 8)", "8");
            Expect(one, "(vl-propagate 'ghnsp)", "8");
            Expect(one, "(setq ghnsp 9)", "9");

            Expect(open, "ghnsp", "8");
            Expect(one, "ghnsp", "9");

            var later = new LispInterpreter();
            Expect(later, "ghnsp", "8");
        }

        [Fact]
        public void VlPropagate_OfAnUnboundSymbol_GivesNil_AndPropagatesNothing()
        {
            // AutoCAD 2021 (r4): (vl-propagate 'nosuchvar) nil, (vl-propagate "x") ; error: bad argument type: symbolp "x"
            var one = new LispInterpreter();
            var other = new LispInterpreter();

            Expect(one, "(vl-propagate 'ghnsnone)", "nil");
            Expect(other, "(boundp 'ghnsnone)", "nil");
            Expect(one, "(vl-propagate \"x\")", "; error: bad argument type: symbolp \"x\"");
        }

        [Fact]
        public void VlPropagate_TheLastValueOfASymbolWins_AndADefunStaysACommand()
        {
            var one = new LispInterpreter();
            var other = new LispInterpreter();

            one.Run("(defun c:ghnsc () (princ \"one\") (princ))");
            one.Run("(vl-propagate 'c:ghnsc)");
            one.Run("(defun c:ghnsc () (princ \"two\") (princ))");
            one.Run("(vl-propagate 'c:ghnsc)");

            Expect(other, "(boundp 'c:ghnsc)", "T");
            Assert.True(other.IsCommand("ghnsc"));
            Assert.Equal("two", other.RunCommand("ghnsc").Output);

            var later = new LispInterpreter();
            Expect(later, "(c:ghnsc)", "two");
            Assert.True(later.IsCommand("ghnsc"));
        }

        [Fact]
        public void VlLoadAll_LoadsTheFileNow_AndInEveryOtherInterpreterOnce()
        {
            // AutoCAD 2021 (r4): (vl-load-all "nosuch.lsp") ; error: LOAD failed: "nosuch.lsp"
            string path = File(
                "ghnsload.lsp",
                "(setq ghnscount (1+ (if ghnscount ghnscount 0)))\n(defun ghnsfn () 7)\n42\n"
            );
            var one = new LispInterpreter();
            var open = new LispInterpreter();

            Expect(one, "(vl-load-all \"" + path + "\")", "42");
            Expect(one, "ghnscount", "1");
            Expect(open, "(ghnsfn)", "7");
            Expect(open, "ghnscount", "1");

            var later = new LispInterpreter();
            Expect(later, "ghnscount", "1");
            Assert.Contains(Path.GetFullPath(path), open.LoadedFiles);
            Assert.Contains(Path.GetFullPath(path), later.LoadedFiles);
        }

        [Fact]
        public void VlLoadAll_OfAMissingFile_IsALoadError_AndHandsNothingOver()
        {
            // AutoCAD 2021 (r4): ; error: LOAD failed: "nosuch.lsp"
            var one = new LispInterpreter();
            var other = new LispInterpreter();

            Expect(one, "(vl-load-all \"nosuch.lsp\")", "; error: LOAD failed: \"nosuch.lsp\"");
            Expect(other, "(boundp 'ghnsnothing)", "nil");
            Assert.Empty(other.LoadedFiles);
        }

        [Fact]
        public void VlLoadAll_AFileThatFailsInAnotherInterpreter_DoesNotSpoilItsNextEvaluation()
        {
            // ours: D119, nobody is there to read an error of a file another document loaded
            string path = File("ghnsfail.lsp", "(if ghnsonlyhere (setq ghnsfailed 1) (car 5))\n");
            var one = new LispInterpreter();
            var other = new LispInterpreter();
            one.Run("(setq ghnsonlyhere T)");

            Expect(one, "(vl-load-all \"" + path + "\")", "1");

            LispResult next = other.Run("(+ 1 2)");
            Assert.False(next.Failed);
            Assert.Equal("3", next.Output);
            Expect(other, "(boundp 'ghnsfailed)", "nil");
        }

        [Fact]
        public void VlDocRef_AndVlDocSet_ReadAndSetASymbolOfThisDocument()
        {
            // AutoCAD 2021 (r4): (setq ghd 7) (vl-doc-ref 'ghd) 7, (vl-doc-set 'ghd 8) 8, !ghd 8, (vl-doc-set 'ghd2 9) 9, !ghd2 9, (vl-doc-ref "ghd") 8
            var lisp = new LispInterpreter();

            Expect(lisp, "(setq ghnsd 7)", "7");
            Expect(lisp, "(vl-doc-ref 'ghnsd)", "7");
            Expect(lisp, "(vl-doc-set 'ghnsd 8)", "8");
            Expect(lisp, "ghnsd", "8");
            Expect(lisp, "(vl-doc-set 'ghnsd2 9)", "9");
            Expect(lisp, "ghnsd2", "9");
            Expect(lisp, "(vl-doc-ref \"ghnsd\")", "8");
            Expect(lisp, "(vl-doc-ref 'ghnsnone)", "nil");
        }

        [Fact]
        public void VlDocSet_DoesNotReachAnotherInterpreter()
        {
            var one = new LispInterpreter();
            var two = new LispInterpreter();

            Expect(one, "(vl-doc-set 'ghnsonly 1)", "1");
            Expect(two, "(vl-doc-ref 'ghnsonly)", "nil");
        }

        [Fact]
        public void TheVlxFunctions_AnswerAsAutoCadDoesWithNoVlxLoaded()
        {
            // AutoCAD 2021 (r4): (vl-doc-export 'foo) T, (vl-doc-import "foo") ; error: LISP Application is not found foo,
            // (vl-vlx-loaded-p "x") nil, (vl-unload-vlx "x") ; error: LISP Application is not found x,
            // (vl-list-loaded-vlx) nil, (vl-list-exported-functions) ("FOO"), (vl-get-resource "x") nil
            var lisp = new LispInterpreter();

            Expect(lisp, "(vl-list-exported-functions)", "nil");
            Expect(lisp, "(vl-doc-export 'foo)", "T");
            Expect(lisp, "(vl-doc-import \"foo\")", "; error: LISP Application is not found foo");
            Expect(lisp, "(vl-vlx-loaded-p \"x\")", "nil");
            Expect(lisp, "(vl-unload-vlx \"x\")", "; error: LISP Application is not found x");
            Expect(lisp, "(vl-list-loaded-vlx)", "nil");
            Expect(lisp, "(vl-list-exported-functions)", "(\"FOO\")");
            Expect(lisp, "(vl-get-resource \"x\")", "nil");
        }

        [Fact]
        public void AFunctionExportedTwice_IsListedOnce_InTheOrderExported()
        {
            var lisp = new LispInterpreter();

            lisp.Run("(vl-doc-export 'beta)");
            lisp.Run("(vl-doc-export \"alpha\")");
            lisp.Run("(vl-doc-export 'beta)");

            Expect(lisp, "(vl-list-exported-functions)", "(\"BETA\" \"ALPHA\")");
        }

        [Theory]
        [InlineData("(vl-vbaload \"x.dvb\")", "vl-vbaload: VBA is not supported by GeometryHelper.Cad")] // AutoCAD 2021 (r4): ; error: bad argument type: VLA-OBJECT nil
        [InlineData("(vl-vbarun \"x\")", "vl-vbarun: VBA is not supported by GeometryHelper.Cad")] // AutoCAD 2021 (r4): T
        [InlineData("(vl-arx-import 'foo)", "vl-arx-import: ObjectARX is not supported by GeometryHelper.Cad")] // AutoCAD 2021 (r4): nil
        [InlineData("(vl-acad-defun 'foo)", "vl-acad-defun: ObjectARX is not supported by GeometryHelper.Cad")] // AutoCAD 2021 (r4): 2047
        [InlineData("(vl-acad-undefun 'foo)", "vl-acad-undefun: ObjectARX is not supported by GeometryHelper.Cad")] // ours: D122
        [InlineData("(vl-vbarun)", "vl-vbarun: VBA is not supported by GeometryHelper.Cad")] // ours: D122, refused whatever the arguments
        public void TheFunctionsOfAutoCadAlone_AreRefusedNamingTheFunction(string expression, string message)
        {
            Expect(new LispInterpreter(), expression, "; error: " + message);
        }
    }
}
