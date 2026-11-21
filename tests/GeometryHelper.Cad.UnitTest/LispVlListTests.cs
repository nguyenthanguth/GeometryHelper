using System.Diagnostics;
using GeometryHelper.Cad.Lisp;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The list functions with a <c>vl-</c> name (<c>vl-sort</c>, <c>vl-remove</c>, <c>vl-position</c>, <c>vl-every</c> and
    /// the rest), against AutoCAD 2021's accoreconsole: probes r1 and r5 of the L3 measurements. Each row is an expression
    /// and the text AutoCAD printed for it, an error as the message after <c>; error: </c>.
    /// </summary>
    [Collection("LispSerial")]
    public class LispVlListTests
    {
        private readonly ITestOutputHelper _output;

        public LispVlListTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        [Theory]
        [InlineData("(vl-sort '(3 1 2 3 1) '<)", "(1 2 3)")] // AutoCAD 2021: (1 2 3), the second 3 and 1 dropped (r1)
        [InlineData("(vl-sort '(3.0 1.0 2.0 3.0) '<)", "(1.0 2.0 3.0 3.0)")] // AutoCAD 2021: reals are kept (r1)
        [InlineData("(vl-sort '(3 1.0 2 1 3.0) '<)", "(1.0 1 2 3 3.0)")] // AutoCAD 2021: 1.0 and 1 are not the same kind (r1)
        [InlineData("(vl-sort '(\"b\" \"a\" \"c\" \"a\") '<)", "(\"a\" \"a\" \"b\" \"c\")")] // AutoCAD 2021: strings are kept (r1)
        [InlineData("(vl-sort '(b a c a) '(lambda (x y) (< (vl-symbol-name x) (vl-symbol-name y))))", "(A B C)")] // AutoCAD 2021: a symbol twice is one (r1)
        [InlineData(
            "(vl-sort '((1 3) (2 2) (3 1) (1 3)) '(lambda (a b) (< (cadr a) (cadr b))))",
            "((3 1) (2 2) (1 3) (1 3))"
        )] // AutoCAD 2021: lists are kept (r1)
        [InlineData(
            "(vl-sort '((1 . \"a\") (2 . \"b\") (1 . \"c\") (2 . \"d\") (1 . \"e\")) '(lambda (a b) (< (car a) (car b))))",
            "((1 . \"a\") (1 . \"c\") (1 . \"e\") (2 . \"b\") (2 . \"d\"))"
        )] // AutoCAD 2021: equal keys keep their order (r1)
        [InlineData("(vl-sort '(5 4 3 2 1) '>)", "(5 4 3 2 1)")] // AutoCAD 2021: (5 4 3 2 1) (r1)
        [InlineData("(vl-sort '(1 2 3) '>=)", "(3 2 1)")] // AutoCAD 2021: (3 2 1) (r1)
        [InlineData("(vl-sort nil '<)", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-sort '(3 1 2) (function <))", "(1 2 3)")] // AutoCAD 2021: (1 2 3) (r1)
        [InlineData("(vl-sort '(3 2 3 1 3) '>)", "(3 2 1)")] // AutoCAD 2021: (3 2 1) (r5)
        [InlineData("(vl-sort '(\"a\" \"a\") '<)", "(\"a\" \"a\")")] // AutoCAD 2021: (a a) kept (r5)
        [InlineData("(vl-sort (list 1 1 (+ 0 1)) '<)", "(1)")] // AutoCAD 2021: (1) (r5)
        [InlineData("(vl-sort '(1 2 1) '(lambda (a b) nil))", "(1 2 1)")] // AutoCAD 2021: (1 2 1), a function that is always nil leaves the order (r5)
        [InlineData("(vl-sort '(b a b) '(lambda (a b) nil))", "(B A B)")] // AutoCAD 2021: (B A B) (r5)
        [InlineData("(vl-sort '(4 3 2 1) '(lambda (a b) T))", "(1 2 3 4)")] // AutoCAD 2021: (1 2 3 4), always T reverses (r5)
        [InlineData("(vl-sort '(1 2 3 4) '(lambda (a b) nil))", "(1 2 3 4)")] // AutoCAD 2021: (1 2 3 4) (r5)
        [InlineData("(vl-sort '(3 1 2) 'list)", "(2 1 3)")] // AutoCAD 2021: (2 1 3), list is always non-nil (r5)
        public void VlSort_SortsStablyAndDropsRepeatedIntegersAndSymbols(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void VlSort_LeavesTheListItWasGivenAlone()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq ghl '(3 1 2))");

            // AutoCAD 2021: (1 2 3), and !ghl still (3 1 2) (r1; ghl is read as a variable here)
            Assert.Equal("(1 2 3)", lisp.Run("(vl-sort ghl '<)").Output);
            Assert.Equal("(3 1 2)", lisp.Run("ghl").Output);
        }

        [Theory]
        [InlineData("(vl-sort-i '(3 1 2 3 1) '<)", "(4 1 2 3 0)")] // AutoCAD 2021: (4 1 2 3 0) (r1)
        [InlineData("(vl-sort-i '(3 1 2 3 1) '>)", "(3 0 2 4 1)")] // AutoCAD 2021: (3 0 2 4 1) (r1)
        [InlineData("(vl-sort-i '(\"b\" \"a\" \"c\" \"a\") '<)", "(3 1 0 2)")] // AutoCAD 2021: (3 1 0 2) (r1)
        [InlineData(
            "(vl-sort-i '((1 . \"a\") (2 . \"b\") (1 . \"c\") (2 . \"d\") (1 . \"e\")) '(lambda (a b) (< (car a) (car b))))",
            "(4 2 0 3 1)"
        )] // AutoCAD 2021: (4 2 0 3 1) (r1)
        [InlineData("(vl-sort-i nil '<)", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-sort-i '(4 3 2 1) '(lambda (a b) T))", "(0 1 2 3)")] // AutoCAD 2021: (0 1 2 3) (r5)
        [InlineData("(vl-sort-i '(1 2 3 4) '(lambda (a b) nil))", "(3 2 1 0)")] // AutoCAD 2021: (3 2 1 0) (r5)
        public void VlSortI_GivesTheIndicesWithEqualItemsLastOneFirst(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-sort '(1 \"a\") '<)", "bad argument type for compare: \"a\" 1")] // AutoCAD 2021: ; error: bad argument type for compare: "a" 1 (r1)
        [InlineData("(vl-sort 5 '<)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-sort '(2 1) 'nosuch)", "bad function: NOSUCH")] // AutoCAD 2021: ; error: bad function: NOSUCH (r1)
        [InlineData("(vl-sort '(2 1) \"a\")", "bad function: \"a\"")] // AutoCAD 2021: ; error: bad function: "a" (r1)
        [InlineData("(vl-remove 1 5)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-remove-if 'nosuch '(1 2))", "bad function: NOSUCH")] // AutoCAD 2021: ; error: bad function: NOSUCH (r1)
        [InlineData("(vl-position 1 5)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-position 1 '(0 . 1))", "bad list: 1")] // AutoCAD 2021: ; error: bad list: 1 (r1)
        [InlineData("(vl-list*)", "too few arguments")] // AutoCAD 2021: ; error: too few arguments (r1)
        [InlineData("(vl-list-length 5)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-every '= '(1 2) 5)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-member-if 'minusp 5)", "bad argument type: listp 5")] // AutoCAD 2021: ; error: bad argument type: listp 5 (r1)
        [InlineData("(vl-every 'numberp '(1 . 2))", "bad list: 2")] // AutoCAD 2021: ; error: bad list: 2 (r5)
        public void TheVlListFunctions_RefuseWhatAutoCadRefuses(string expression, string message)
        {
            LispResult result = new LispInterpreter().Run(expression);

            Assert.Equal("; error: " + message, result.Output);
            Assert.True(result.Failed);
            Assert.Equal(message, result.Error);
        }

        [Theory]
        [InlineData("(vl-remove 1 '(1 2 1 3))", "(2 3)")] // AutoCAD 2021: (2 3) (r1)
        [InlineData("(vl-remove 1.0 '(1 2 1.0 3))", "(1 2 3)")] // AutoCAD 2021: (1 2 3), 1.0 only against a real (r1)
        [InlineData("(vl-remove \"a\" '(\"a\" \"b\" \"A\"))", "(\"b\" \"A\")")] // AutoCAD 2021: ("b" "A") (r1)
        [InlineData("(vl-remove '(1 2) '((1 2) (3) (1 2)))", "((3))")] // AutoCAD 2021: ((3)) (r1)
        [InlineData("(vl-remove 1 nil)", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-remove 'a '(a b a))", "(B)")] // AutoCAD 2021: (B) (r5)
        [InlineData("(vl-remove nil '(1 nil 2))", "(1 2)")] // AutoCAD 2021: (1 2) (r5)
        [InlineData("(vl-remove-if 'minusp '(1 -2 3 -4))", "(1 3)")] // AutoCAD 2021: (1 3) (r1)
        [InlineData("(vl-remove-if-not 'minusp '(1 -2 3 -4))", "(-2 -4)")] // AutoCAD 2021: (-2 -4) (r1)
        [InlineData("(vl-remove-if '(lambda (x) (= x 2)) '(1 2 3))", "(1 3)")] // AutoCAD 2021: (1 3) (r1)
        public void VlRemove_TakesOutWhatMatches(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-member-if 'minusp '(1 -2 3))", "(-2 3)")] // AutoCAD 2021: (-2 3) (r1)
        [InlineData("(vl-member-if-not 'minusp '(-1 -2 3 4))", "(3 4)")] // AutoCAD 2021: (3 4) (r1)
        [InlineData("(vl-member-if 'minusp '(1 2))", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-member-if '(lambda (x) T) '(1 . 2))", "(1 . 2)")] // AutoCAD 2021: (1 . 2), the match comes first (r5)
        public void VlMemberIf_GivesTheTailFromTheFirstMatch(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-position 3 '(1 2 3 3))", "2")] // AutoCAD 2021: 2 (r1)
        [InlineData("(vl-position 3.0 '(1 2 3))", "nil")] // AutoCAD 2021: nil, a real is not an integer here (r1)
        [InlineData("(vl-position '(1 2) '((0) (1 2)))", "1")] // AutoCAD 2021: 1 (r1)
        [InlineData("(vl-position \"b\" '(\"a\" \"B\" \"b\"))", "2")] // AutoCAD 2021: 2 (r1)
        [InlineData("(vl-position 9 '(1 2))", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-position 1 nil)", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-position (list 1 2.0) '((1 2)))", "nil")] // AutoCAD 2021: nil, 2.0 is not 2 inside a list either (r5)
        [InlineData("(vl-position nil '(1 nil))", "1")] // AutoCAD 2021: 1 (r5)
        [InlineData("(member 1.0 '(1 2))", "(1 2)")] // AutoCAD 2021: (1 2), member keeps equal (r5)
        [InlineData("(member 1 '(1.0 2))", "(1.0 2)")] // AutoCAD 2021: (1.0 2) (r5)
        public void VlPosition_ComparesWithTheType_WhereMemberComparesWithEqual(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-some '= '(1 2 3) '(3 2 1))", "T")] // AutoCAD 2021: T (r1)
        [InlineData("(vl-some '(lambda (x) (if (> x 1) (* x 10))) '(1 2 3))", "20")] // AutoCAD 2021: 20, the first value that is not nil (r1)
        [InlineData("(vl-some 'minusp '(1 2))", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-some '= '(1 2 3) '(0 2))", "T")] // AutoCAD 2021: T, the lists stop at the shortest (r1)
        [InlineData("(vl-every '= '(1 2) '(1 2 3))", "T")] // AutoCAD 2021: T (r1)
        [InlineData("(vl-every '= '(1 2 3) '(1 2))", "T")] // AutoCAD 2021: T (r1)
        [InlineData("(vl-every 'minusp nil)", "T")] // AutoCAD 2021: T (r1)
        [InlineData("(vl-every 'numberp '(1 a))", "nil")] // AutoCAD 2021: nil (r1)
        public void VlSomeAndVlEvery_StopAtTheShortestList(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-list* 1 2 3)", "(1 2 . 3)")] // AutoCAD 2021: (1 2 . 3) (r1)
        [InlineData("(vl-list* 1 '(2 3))", "(1 2 3)")] // AutoCAD 2021: (1 2 3) (r1)
        [InlineData("(vl-list* 1)", "1")] // AutoCAD 2021: 1 (r1)
        [InlineData("(vl-list* '(1))", "(1)")] // AutoCAD 2021: (1) (r1)
        [InlineData("(vl-list-length '(1 2 3))", "3")] // AutoCAD 2021: 3 (r1)
        [InlineData("(vl-list-length '(1 2 . 3))", "nil")] // AutoCAD 2021: nil (r1)
        [InlineData("(vl-list-length nil)", "0")] // AutoCAD 2021: 0 (r1)
        [InlineData("(vl-consp nil)", "nil")] // AutoCAD 2021: nil (r1)
        public void VlListStarAndVlListLength_AsAutoCadGivesThem(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void VlSort_OfTenThousandReals_IsSortedWithinAHundredMilliseconds()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(setq i 0 l nil)");
            lisp.Run("(repeat 10000 (setq i (1+ i) l (cons (* 1.0 (rem (* i 7919) 10007)) l)))");
            Assert.Equal("10000", lisp.Run("(length l)").Output);

            // One run first, so the time is of the sort and not of the compiler warming up.
            lisp.Run("(setq s (vl-sort l '<))");
            var watch = Stopwatch.StartNew();
            lisp.Run("(setq s (vl-sort l '<))");
            watch.Stop();
            _output.WriteLine("vl-sort of 10000 reals: " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms");

            Assert.Equal("T", lisp.Run("(vl-every '<= s (cdr s))").Output);
            Assert.Equal("10000", lisp.Run("(length s)").Output);
            Assert.True(watch.ElapsedMilliseconds < 100, watch.ElapsedMilliseconds + " ms");
        }
    }
}
