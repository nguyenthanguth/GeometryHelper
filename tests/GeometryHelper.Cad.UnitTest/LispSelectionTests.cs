using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>ssget</c> with its modes and filters, <c>sslength</c>, <c>ssname</c>, <c>ssadd</c>, <c>ssdel</c> and <c>ssmemb</c>
    /// typed at the command line, with what AutoCAD 2021's accoreconsole gave for the drawing of probe q5 (a LINE on layer GHA from
    /// 0,0 to 10,0, a CIRCLE at 20,0 of radius 5, a TEXT, a polyline and a linear dimension, in that order) and probe q8.
    /// </summary>
    public sealed class LispSelectionTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        [Fact]
        public void SsgetX_TakesEveryObjectOfTheDrawing_TheNewestFirst()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (sslength (ssget "X")) 5; (ssname (ssget "X") 0) is the DIMENSION, the last one made
            Assert.Equal("5", Value(session, "(sslength (ssget \"X\"))"));
            Assert.Equal("\"DIMENSION\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"X\") 0))))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"X\") 4))))"));
            Assert.Equal("T", Value(session, "(eq (ssname (ssget \"_X\") 0) (entlast))"));
        }

        [Theory]
        [InlineData("(ssget \"X\" '((0 . \"LINE\")))", "1")] // AutoCAD 2021 (q5)
        [InlineData("(ssget \"_X\" '((8 . \"GHA\")))", "1")] // AutoCAD 2021 (q5)
        [InlineData("(ssget \"X\" '((-4 . \"<OR\") (0 . \"TEXT\") (0 . \"MTEXT\") (-4 . \"OR>\")))", "1")] // AutoCAD 2021 (q5)
        [InlineData("(ssget \"X\" '((0 . \"TEXT,MTEXT\")))", "1")] // a pattern of wcmatch: alternatives
        [InlineData("(ssget \"X\" '((0 . \"*LINE\")))", "2")] // LINE and LWPOLYLINE
        [InlineData("(ssget \"X\" '((0 . \"line\")))", "1")] // the type is matched whatever the case
        [InlineData("(ssget \"X\" '((8 . \"g*\")))", "1")] // a layer by pattern, case ignored
        [InlineData("(ssget \"X\" '((62 . 256)))", "5")] // BYLAYER is 256 where the object has no colour of its own
        [InlineData("(ssget \"X\" '((-4 . \"<AND\") (0 . \"CIRCLE\") (8 . \"0\") (-4 . \"AND>\")))", "1")]
        [InlineData("(ssget \"X\" '((-4 . \"<AND\") (0 . \"CIRCLE\") (8 . \"GHA\") (-4 . \"AND>\")))", "nil")]
        [InlineData("(ssget \"X\" '((-4 . \"<NOT\") (0 . \"LINE\") (-4 . \"NOT>\")))", "4")]
        [InlineData(
            "(ssget \"X\" '((-4 . \"<NOT\") (-4 . \"<OR\") (0 . \"LINE\") (0 . \"CIRCLE\") (-4 . \"OR>\") (-4 . \"NOT>\")))",
            "3"
        )]
        [InlineData("(ssget \"X\" '((-4 . \"<XOR\") (0 . \"LINE\") (8 . \"GHA\") (-4 . \"XOR>\")))", "nil")] // both true: not exactly one
        [InlineData("(ssget \"X\" '((-4 . \"<XOR\") (0 . \"LINE\") (8 . \"0\") (-4 . \"XOR>\")))", "5")] // LINE on GHA: one; the others on 0: one
        [InlineData("(ssget \"X\" '((-4 . \">=\") (40 . 5.0)))", "1")] // the circle's radius 5.0; the text's 2.5 is lower
        [InlineData("(ssget \"X\" '((-4 . \">\") (40 . 2.5)))", "1")]
        [InlineData("(ssget \"X\" '((-4 . \"<\") (40 . 5.0)))", "3")] // the text 2.5, the polyline and the dimension, whose 40 are 0.0
        [InlineData("(ssget \"X\" '((-4 . \"<=\") (40 . 5.0)))", "4")]
        [InlineData("(ssget \"X\" '((-4 . \"/=\") (40 . 5.0)))", "4")] // the objects with no 40 are not equal to it
        [InlineData("(ssget \"X\" '((-4 . \"!=\") (0 . \"LINE\")))", "4")]
        [InlineData("(ssget \"X\" '((-4 . \"<>\") (0 . \"LINE\")))", "4")]
        [InlineData("(ssget \"X\" '((-4 . \"*\") (40 . 1.0)))", "5")] // * is always true
        [InlineData("(ssget \"X\" '((40 . 5)))", "1")] // a whole number given for a real
        [InlineData("(ssget \"X\" '((10 20.0 0.0 0.0)))", "1")] // a point, the circle's centre
        [InlineData("(ssget \"X\" '((-4 . \">,>,*\") (10 5.0 5.0 0.0)))", "2")] // x > 5 and y > 5: the circle's centre (20 0) fails, the polyline vertex (10 20) and the text (0 10) differ
        [InlineData("(ssget \"X\" '((0 . \"NOSUCH\")))", "nil")] // AutoCAD 2021 (q5)
        [InlineData("(ssget \"X\" '((-3 (\"NOAPP\"))))", "nil")]
        [InlineData("(ssget \"X\" '((410 . \"Model\")))", "5")]
        [InlineData("(ssget \"X\" '((410 . \"Layout1\")))", "nil")]
        [InlineData("(ssget \"X\" '((67 . 0)))", "5")]
        public void SsgetXWithAFilter_KeepsTheObjectsThatMatch(string form, string count)
        {
            CadCommandSession session = Q5();

            // ssget gives a set or nil; the count of the set is written here, nil as it is
            string result = Value(session, "(if (setq s " + form + ") (sslength s))");

            Assert.Equal(count, result);
        }

        [Fact]
        public void TheBitOperatorsOfAFilter_TestTheBitsOfAWholeNumber()
        {
            CadCommandSession session = Q5();
            Value(
                session,
                "(entmake '((0 . \"LWPOLYLINE\") (90 . 3) (70 . 1) (10 0.0 0.0) (10 5.0 0.0) (10 5.0 5.0)))"
            );

            // 70 of the three open polylines is 0, of the one made 1 (closed)
            Assert.Equal("1", Value(session, "(sslength (ssget \"X\" '((-4 . \"&\") (70 . 1))))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"X\" '((-4 . \"&=\") (70 . 1))))"));
            Assert.Equal("nil", Value(session, "(ssget \"X\" '((-4 . \"&\") (70 . 2)))"));
        }

        [Fact]
        public void AFilterThatIsNoListOfGroups_GivesNil()
        {
            CadCommandSession session = Q5();

            Assert.Equal("nil", Value(session, "(ssget \"X\" '((0 . 5)))"));
            Assert.Equal("nil", Value(session, "(ssget \"X\" '(1 2 3))"));
        }

        [Fact]
        public void SsgetWindowAndCrossing_TakeWhatTheBoxHoldsOrTouches()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (ssget "W" '(-1 -1) '(11 1)) the line, (ssget "C" '(-1 -1) '(1 1)) the line
            Assert.Equal("1", Value(session, "(sslength (ssget \"W\" '(-1 -1) '(11 1)))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"C\" '(-1 -1) '(1 1)))"));
            Assert.Equal("nil", Value(session, "(ssget \"W\" '(-1 -1) '(5 1))")); // the line runs out of the window
            Assert.Equal("nil", Value(session, "(ssget \"C\" '(50 50) '(60 60))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"W\" '(11 1) '(-1 -1)))")); // the corners in either order
            Assert.Equal(
                "\"LINE\"",
                Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"W\" '(-1 -1) '(11 1)) 0))))")
            );
        }

        [Fact]
        public void SsgetWindowWithAFilter_KeepsTheObjectsThatMatchToo()
        {
            CadCommandSession session = Q5();

            Assert.Equal("nil", Value(session, "(ssget \"W\" '(-1 -1) '(11 1) '((0 . \"CIRCLE\")))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"C\" '(-100 -100) '(100 100) '((0 . \"CIRCLE\"))))"));
        }

        [Fact]
        public void SsgetFenceAndPolygons_TakeWhatTheyCrossOrHold()
        {
            CadCommandSession session = Q5();

            Assert.Equal("1", Value(session, "(sslength (ssget \"F\" '((5.0 -5.0) (5.0 5.0))))")); // a fence across the line
            Assert.Equal(
                "1",
                Value(session, "(sslength (ssget \"WP\" '((-1.0 -1.0) (11.0 -1.0) (11.0 1.0) (-1.0 1.0))))")
            );
            Assert.Equal("1", Value(session, "(sslength (ssget \"CP\" '((4.0 -1.0) (6.0 -1.0) (6.0 1.0) (4.0 1.0))))"));
            Assert.Equal("nil", Value(session, "(ssget \"WP\" '((4.0 -1.0) (6.0 -1.0) (6.0 1.0) (4.0 1.0)))"));
        }

        [Fact]
        public void SsgetWithAPoint_PicksTheObjectThere()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (ssget '(20 5)) is nil in the console, which picks only near the origin; the presenter picks the
            // circle at its top
            Assert.Equal("\"CIRCLE\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget '(20 5)) 0))))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"X\" '((0 . \"LINE\"))))"));
            Assert.Equal("nil", Value(session, "(ssget '(100 100))"));
        }

        [Fact]
        public void SsgetLastAndPrevious_AreTheLastObjectAndWhatTheLastSelectionTook()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (ssget "L") one object, then (ssget "P") one
            Assert.Equal("1", Value(session, "(sslength (ssget \"L\"))"));
            Assert.Equal("\"DIMENSION\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"L\") 0))))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"P\"))"));
        }

        [Fact]
        public void EverySetSsgetGives_BecomesThePreviousSelection()
        {
            CadCommandSession session = Q5();

            Value(session, "(ssget \"X\" '((0 . \"CIRCLE\")))");

            // AutoCAD 2021 (q5; runs/ll): the set ssget gave is what (ssget "P") and a "p" at Select objects: take
            Assert.Equal("\"CIRCLE\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"P\") 0))))"));
            Assert.Equal("1", Value(session, "(sslength (ssget \"P\"))"));

            Value(session, "(ssget \"X\" '((0 . \"TEXT\")))");

            Assert.Equal("\"TEXT\"", Value(session, "(cdr (assoc 0 (entget (ssname (ssget \"P\") 0))))"));
        }

        [Fact]
        public void SsgetPreviousOfWhatWasErased_LeavesOutTheObjectsThatAreGone()
        {
            CadCommandSession session = Q5();
            Value(session, "(ssget \"X\" '((0 . \"CIRCLE\")))");
            Value(session, "(entdel (ssname (ssget \"P\") 0))");

            Assert.Equal("nil", Value(session, "(ssget \"P\")"));
        }

        [Fact]
        public void SsgetPreviousTypedAtASelectPrompt_TakesWhatSsgetGave()
        {
            CadCommandSession session = Q5();
            Value(session, "(ssget \"X\" '((0 . \"CIRCLE\")))");

            Run(session, "ERASE", "P", Enter);

            Assert.Equal("nil", Value(session, "(ssget \"X\" '((0 . \"CIRCLE\")))"));
            Assert.Equal("4", Value(session, "(sslength (ssget \"X\"))"));
        }

        [Fact]
        public void SsgetImplied_TakesTheObjectsSelectedBefore()
        {
            CadCommandSession session = Q5();
            session.Document.Selection.Set(new[] { session.Document.ModelSpace.Block.First().Handle });

            Assert.Equal("1", Value(session, "(sslength (ssget \"I\"))"));
        }

        [Fact]
        public void TheSetFunctions_MakeAddDeleteAndTestSets()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): (setq s (ssadd)) a set, (sslength s) 0, (sslength (ssadd e1 s)) 1, (ssmemb e1 s) e1,
            // (sslength (ssdel e1 s)) 0
            Assert.StartsWith("<Selection set: ", Value(session, "(setq s (ssadd))"));
            Assert.Equal("0", Value(session, "(sslength s)"));
            Assert.Equal("1", Value(session, "(sslength (ssadd e1 s))"));
            Assert.Equal("T", Value(session, "(eq (ssmemb e1 s) e1)"));
            Assert.Equal("0", Value(session, "(sslength (ssdel e1 s))"));
            Assert.Equal("nil", Value(session, "(ssmemb e1 s)"));
        }

        [Fact]
        public void SsaddAddsInPlace_AndANewSetWhereNoneIsGiven()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");
            Value(session, "(setq s (ssadd))");

            Value(session, "(ssadd e1 s)");
            Value(session, "(ssadd (entlast) s)");
            Value(session, "(ssadd e1 s)");

            Assert.Equal("2", Value(session, "(sslength s)")); // a name already in the set is not added twice
            Assert.Equal("1", Value(session, "(sslength (ssadd e1))"));
            Assert.Equal("T", Value(session, "(eq (ssname s 0) e1)"));
            Assert.Equal("T", Value(session, "(eq (ssname s 1) (entlast))"));
        }

        [Fact]
        public void SsdelOfANameThatIsNotInTheSet_IsNil()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");
            Value(session, "(setq s (ssadd))");

            Assert.Equal("nil", Value(session, "(ssdel e1 s)"));
        }

        [Fact]
        public void SsnameOutOfRange_IsNil()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (ssname (ssget "X") 99) nil, (ssname (ssget "X") -1) nil
            Assert.Equal("nil", Value(session, "(ssname (ssget \"X\") 99)"));
            Assert.Equal("nil", Value(session, "(ssname (ssget \"X\") -1)"));
        }

        [Theory]
        [InlineData("(sslength nil)", "; error: bad argument type: lselsetp nil")] // AutoCAD 2021 (q4)
        [InlineData("(sslength 5)", "; error: bad argument type: lselsetp 5")]
        [InlineData("(ssname nil 0)", "; error: bad argument type: lselsetp nil")]
        [InlineData("(ssadd 5)", "; error: bad argument type: lentityp 5")]
        [InlineData("(ssadd (entnext) nil)", "; error: bad argument type: lselsetp nil")]
        [InlineData("(ssmemb nil nil)", "; error: bad argument type: lentityp nil")]
        [InlineData("(ssname (ssget \"X\") \"a\")", "; error: bad argument type: fixnump: \"a\"")]
        public void ASetFunctionGivenWhatIsNoSet_IsAnError(string form, string expected)
        {
            CadCommandSession session = Q5();

            Assert.Equal(expected, Value(session, form));
        }

        [Fact]
        public void ASetPrintsItsNumber_AndEachSetMadeHasAnotherOne()
        {
            CadCommandSession session = Q5();

            string first = Value(session, "(ssadd)");
            string second = Value(session, "(ssadd)");

            Assert.StartsWith("<Selection set: ", first);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void SsgetWithNoMode_AsksSelectObjectsUntilEnter_AndGivesTheSet()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (setq s2 (ssget)) then ALL gives 5 found, Enter ends; (sslength s2) 5
            Value(session, "(setq s2 (ssget))");

            Assert.Equal("Select objects:", session.PromptText);

            Run(session, "ALL");

            Assert.Contains("5 found", session.History.Select(l => l.Trim()));

            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("5", Value(session, "(sslength s2)"));
        }

        [Fact]
        public void SsgetOfAllAtTheSelectPrompt_GivesTheNewestObjectFirst_AsSsgetXDoes()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (runs/hhh, runs/nnn): a routine that walks the set of ALL met the object made last first
            Value(session, "(setq s2 (ssget))");
            Run(session, "ALL", Enter);

            Assert.Equal("5", Value(session, "(sslength s2)"));
            Assert.Equal("\"DIMENSION\"", Value(session, "(cdr (assoc 0 (entget (ssname s2 0))))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (ssname s2 4))))"));
        }

        [Fact]
        public void SsgetWithAFilterAndNoMode_CountsOnlyTheObjectsThatMatch()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (setq s3 (ssget '((0 . "CIRCLE")))) with ALL says 1 found, (sslength s3) 1
            Value(session, "(setq s3 (ssget '((0 . \"CIRCLE\"))))");
            Run(session, "ALL");

            Assert.Contains("1 found", session.History.Select(l => l.Trim()));

            Run(session, Enter);

            Assert.Equal("1", Value(session, "(sslength s3)"));
        }

        [Fact]
        public void SsgetWithNothingSelected_IsNil_SoALoopOnItEnds()
        {
            CadCommandSession session = Q5();

            // runs/hh: (while (setq dt (ssget)) ...) ends at the Enter that selects nothing
            Value(session, "(setq dt (ssget))");
            Run(session, Enter);

            Assert.Equal("nil", Value(session, "!dt"));
        }

        [Fact]
        public void SsgetSingle_EndsAtTheFirstPick()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (spec D48): ":S" one pick or one window ends the selection
            Value(session, "(setq s4 (ssget \":S\"))");
            Run(session, "0,0");

            Assert.False(session.IsRunning);
            Assert.Equal("1", Value(session, "(sslength s4)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (ssname s4 0))))"));
        }

        [Fact]
        public void SsgetOfAWindow_TypedAtThePrompt_GivesTheObjectsInIt()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q8): GHS then W, -1,-1, 11,1, Enter gives 1 found
            Value(session, "(setq s (ssget))");
            Run(session, "W", "-1,-1", "11,1", Enter);

            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void TheSetOfSsget_IsThePreviousSelectionOfACommandToo()
        {
            CadCommandSession session = Q5();

            Value(session, "(setq s (ssget))");
            Run(session, "ALL", Enter);
            Run(session, "ERASE", "P", Enter);

            Assert.Equal("nil", Value(session, "(ssget \"X\")"));
        }
    }
}
