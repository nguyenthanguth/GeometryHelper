using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The order of the objects a fence selects (I5 of L9): <c>ssget "_F"</c> and a Fence typed at an <c>ssget</c> prompt give them in
    /// the order of their first crossing along the fence, and Previous after a command's typed fence gives them newest first. The
    /// expected values are those of AutoCAD 2021's accoreconsole, rows of <c>scratch/l9/m/console.md</c> M1-a (feeds
    /// <c>fence.feed</c>, <c>fence2.feed</c> and <c>fence3.feed</c>), marked <c>AutoCAD 2021 (l9 M1-a)</c>. No test puts two
    /// objects through one crossing point: AutoCAD's order there is decided by float error, not by a rule.
    /// </summary>
    public sealed class LispL9FenceOrderTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Start()
        {
            CadCommandSession session = Session(Blank());
            session.Tolerance = Tolerance.Default;
            return session;
        }

        // What a form gives, as the lines after its echo.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        // Lines from (0, y) to (20, y) at the given y, made in the order given, and gy: the type and the y of the first point of each
        // object of a set, in the order the set holds them.
        private static CadCommandSession LinesAt(params int[] ys)
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(foreach y '("
                    + string.Join(" ", ys)
                    + ") (entmake (list '(0 . \"LINE\") (list 10 0.0 (float y) 0.0) (list 11 20.0 (float y) 0.0))))"
            );
            Value(
                session,
                "(defun gy (ss / i r e) (if ss (progn (setq i 0 r nil) (repeat (sslength ss) (setq e (entget (ssname ss i)) r (cons (strcat (cdr (assoc 0 e)) \":\" (rtos (nth 1 (cdr (assoc 10 e))) 2 0)) r) i (1+ i))) (reverse r)) 'NOSET))"
            );
            return session;
        }

        private static CadCommandSession FiveLines() => LinesAt(0, 10, 20, 30, 40);

        private const string Down = "(\"LINE:40\" \"LINE:30\" \"LINE:20\" \"LINE:10\" \"LINE:0\")";
        private const string Up = "(\"LINE:0\" \"LINE:10\" \"LINE:20\" \"LINE:30\" \"LINE:40\")";

        [Fact]
        public void AFenceGoingDown_GivesTheTopLineFirst()
        {
            // AutoCAD 2021 (l9 M1-a fence down): five lines at y 0 to 40 made oldest y 0, a fence from (5, 45) to (5, -5) gives y 40
            // 30 20 10 0; ours (pin) gave drawing order, 0 10 20 30 40.
            Assert.Equal(Down, Value(FiveLines(), "(gy (ssget \"_F\" '((5.0 45.0) (5.0 -5.0))))"));
        }

        [Fact]
        public void AFenceGoingUp_GivesTheBottomLineFirst()
        {
            // AutoCAD 2021 (l9 M1-a fence up): from (5, -5) to (5, 45) gives y 0 10 20 30 40. Green on the pin, where fence order and
            // drawing order are the same.
            Assert.Equal(Up, Value(FiveLines(), "(gy (ssget \"_F\" '((5.0 -5.0) (5.0 45.0))))"));
        }

        [Fact]
        public void AFenceThatTurnsBack_GivesWhatItCrossedGoingDownThenWhatOnlyTheSecondLegMeets()
        {
            // AutoCAD 2021 (l9 M1-a fence down then up): (5, 25) (5, -5) (15, -5) (15, 45) gives y 20 10 0 30 40.
            Assert.Equal(
                "(\"LINE:20\" \"LINE:10\" \"LINE:0\" \"LINE:30\" \"LINE:40\")",
                Value(FiveLines(), "(gy (ssget \"_F\" '((5.0 25.0) (5.0 -5.0) (15.0 -5.0) (15.0 45.0))))")
            );
        }

        [Theory]
        [InlineData("((5.0 -5.0) (5.0 15.0) (15.0 15.0) (15.0 -5.0))", "(\"LINE:0\" \"LINE:10\")")]
        [InlineData("((5.0 25.0) (5.0 5.0) (15.0 5.0) (15.0 25.0))", "(\"LINE:20\" \"LINE:10\")")]
        [InlineData("((5.0 15.0) (5.0 -5.0))", "(\"LINE:10\" \"LINE:0\")")]
        public void AFenceWithAMiddlePoint_ListsALineCrossedTwiceOnceAtItsFirstCrossing(string fence, string expected)
        {
            // AutoCAD 2021 (l9 M1-a fence2 middle point): over the lines y 0, 10 and 20 these three fences give y 0 10, y 20 10 and
            // y 10 0; the first one is green on the pin, where drawing order happens to be the same.
            Assert.Equal(expected, Value(LinesAt(0, 10, 20), "(gy (ssget \"_F\" '" + fence + "))"));
        }

        [Theory]
        [InlineData("((5.0 0.0) (6.0 12.0))", "(\"LINE:5\" \"LINE:6\")")]
        [InlineData("((6.0 12.0) (5.0 0.0))", "(\"LINE:6\" \"LINE:5\")")]
        [InlineData("((5.0 12.0) (6.0 0.0))", "(\"LINE:6\" \"LINE:5\")")]
        public void ASlantedFenceAcrossTwoCloseLines_GivesThemInTheOrderItMeetsThem(string fence, string expected)
        {
            // AutoCAD 2021 (l9 M1-a fence2 parallel lines): lines at y 5 (made first) and y 6, fences (5, 0) to (6, 12) give y 5 6;
            // (6, 12) to (5, 0) and (5, 12) to (6, 0) give y 6 5. The first row is green on the pin.
            Assert.Equal(expected, Value(LinesAt(5, 6), "(gy (ssget \"_F\" '" + fence + "))"));
        }

        // A polyline (0,10) (20,10) (20,30) (0,30) that a vertical fence at x 5 crosses at y 10 and at y 30, made first; then a line at
        // y 40, then a line at y 20.
        private static CadCommandSession PolylineAndTwoLines()
        {
            CadCommandSession session = LinesAt();
            Value(
                session,
                "(entmake '((0 . \"LWPOLYLINE\") (100 . \"AcDbEntity\") (100 . \"AcDbPolyline\") (90 . 4) (70 . 0) (10 0.0 10.0) (10 20.0 10.0) (10 20.0 30.0) (10 0.0 30.0)))"
            );
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 40.0 0.0) (11 20.0 40.0 0.0)))");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 20.0 0.0) (11 20.0 20.0 0.0)))");
            return session;
        }

        [Fact]
        public void APolylineCrossedTwiceByAFenceGoingUp_StandsAtItsFirstCrossing_AndIsListedOnce()
        {
            CadCommandSession session = PolylineAndTwoLines();

            // AutoCAD 2021 (l9 M1-a polyline): fence up gives the polyline (first crossing y 10), the line at 20, the line at 40, and
            // the set holds 3 objects; ours (pin) gave drawing order: polyline, 40, 20.
            Assert.Equal(
                "(\"LWPOLYLINE:10\" \"LINE:20\" \"LINE:40\")",
                Value(session, "(gy (ssget \"_F\" '((5.0 -5.0) (5.0 45.0))))")
            );
            Assert.Equal("3", Value(session, "(sslength (ssget \"_F\" '((5.0 -5.0) (5.0 45.0))))"));
        }

        [Fact]
        public void APolylineCrossedTwiceByAFenceGoingDown_StandsBetweenTheLines_ByItsFirstCrossingFromThere()
        {
            // AutoCAD 2021 (l9 M1-a polyline): fence down gives the line at 40, the polyline (first crossing now y 30), the line at
            // 20; not the polyline by its last crossing, drawing order or newest first (the four orders all differ here). Ours (pin)
            // gave drawing order, polyline first.
            Assert.Equal(
                "(\"LINE:40\" \"LWPOLYLINE:10\" \"LINE:20\")",
                Value(PolylineAndTwoLines(), "(gy (ssget \"_F\" '((5.0 45.0) (5.0 -5.0))))")
            );
        }

        [Theory]
        [InlineData("5,45", "5,-5", Down)]
        [InlineData("5,-5", "5,45", Up)]
        public void AFenceTypedAtTheSelectPromptOfSsget_GivesTheObjectsAlongTheFence(
            string first,
            string second,
            string expected
        )
        {
            CadCommandSession session = FiveLines();

            // AutoCAD 2021 (l9 M1-a fence3): (setq ss (ssget)), F, the two points, Enter, Enter gives y 40 30 20 10 0 going down and
            // 0 10 20 30 40 going up; ours (pin) gave the second order for both, so the first row is red on the pin.
            Value(session, "(setq ss (ssget))");
            Run(session, "F", first, second, Enter, Enter);

            Assert.Equal(expected, Value(session, "(gy ss)"));
        }

        [Theory]
        [InlineData("(ssget \"_F\" '((5.0 45.0) (5.0 -5.0)))", Down)]
        [InlineData("(ssget \"_F\" '((5.0 -5.0) (5.0 45.0)))", Up)]
        public void PreviousAfterAFenceOfSsget_IsThatSetInTheSameOrder(string fence, string expected)
        {
            CadCommandSession session = FiveLines();

            // AutoCAD 2021 (l9 M1-a fence3): (ssget "_F" down) then (ssget "_P") gives y 40 30 20 10 0; going up, 0 10 20 30 40;
            // ours (pin) gave 0 10 20 30 40 for both, so the first row is red on the pin.
            Value(session, fence);

            Assert.Equal(expected, Value(session, "(gy (ssget \"_P\"))"));
        }

        [Theory]
        [InlineData("5,45", "5,-5")]
        [InlineData("5,-5", "5,45")]
        public void PreviousAfterAFenceTypedAtACommand_GivesTheObjectsNewestFirst_WhicheverWayTheFenceRan(
            string first,
            string second
        )
        {
            CadCommandSession session = FiveLines();

            // AutoCAD 2021 (l9 M1-a, MOVE rows): MOVE, F, the two points, Enter, Enter, Esc, then (ssget "_P") gives y 40 30 20 10 0
            // whether the fence went down or up; ours (pin) gave 0 10 20 30 40 for both.
            Run(session, "MOVE", "F", first, second, Enter, Enter, Esc);

            Assert.Equal(Down, Value(session, "(gy (ssget \"_P\"))"));
        }
    }
}
