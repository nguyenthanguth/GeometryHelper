using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The words U, UNDO and REDO write for a step, and which changes a step of a routine holds (I4 of L9). The expected values are
    /// those of AutoCAD 2021's accoreconsole, rows u1 to u8 and r1 to r3 of <c>scratch/l9/m/console.md</c> M1-b (feeds
    /// <c>undo-u*.feed</c>, <c>undo-r*.feed</c>), marked <c>AutoCAD 2021 (l9 M1-b uN)</c>. Each row starts from a drawing with one line
    /// at y -20 made by a command, the step a U can name.
    /// </summary>
    public sealed class LispL9UndoMessageTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        // A blank drawing with one line from (0, -20) to (10, -20) and three routines: gh9a runs a line and then a circle, gh9c runs a
        // line and then U, and gh9d runs one circle.
        private static CadCommandSession Start()
        {
            CadCommandSession session = Session(Blank());
            session.Tolerance = Tolerance.Default;
            Run(session, "_.LINE", "0,-20", "10,-20", Enter);
            session.Feed(
                "(defun c:gh9a () (command \"_.LINE\" \"0,20\" \"10,20\" \"\") (command \"_.CIRCLE\" \"5,20\" \"3\") (princ))"
            );
            session.Feed("(defun c:gh9c () (command \"_.LINE\" \"0,30\" \"10,30\" \"\") (command \"_.U\") (princ))");
            session.Feed("(defun c:gh9d () (command \"_.CIRCLE\" \"5,30\" \"3\") (princ))");
            return session;
        }

        // The last line the session wrote.
        private static string Said(CadCommandSession session) => session.History[session.History.Count - 1].TrimEnd();

        // The first line after the echo of a typed form.
        private static string FirstLineOf(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return session.History[before + 1].TrimEnd();
        }

        // U, or UNDO with 1.
        private static void UndoOnce(CadCommandSession session, string typed)
        {
            if (typed == "U")
            {
                Run(session, "U");
            }
            else
            {
                Run(session, "UNDO", "1");
            }
        }

        // The drawing's objects, as the (ssget "_X") the console would count.
        private static string Count(CadCommandSession session)
        {
            int before = session.History.Count;
            session.Feed("(if (ssget \"_X\") (sslength (ssget \"_X\")) 0)");
            return session.History[before + 1].TrimEnd();
        }

        [Theory]
        [InlineData("L")]
        [InlineData("_.LINE")]
        public void UAfterALine_NamesTheLine(string command)
        {
            CadCommandSession session = Start();
            Run(session, command, "0,5", "10,5", Enter);

            Run(session, "U");

            // AutoCAD 2021 (l9 M1-b u1, u2): U after L (the alias) and after _.LINE writes LINE. Green on the pin.
            Assert.Equal("LINE", Said(session));
        }

        [Theory]
        [InlineData("GH9A")]
        [InlineData("gh9a")]
        public void UAfterARoutineOfTwoCommands_NamesTheCircleThenTheRoutine(string typed)
        {
            CadCommandSession session = Start();
            Run(session, typed);

            // AutoCAD 2021 (l9 M1-b u3, u3c): the circle is undone first and is the step "(Lisp Expression) GROUP"; the line before
            // it is "GH9A GROUP", the name in capitals though typed in lower case; one object (the line of the drawing's start)
            // is left after the two. Ours (pin) wrote CIRCLE and LINE; the counts, 2 and 1, are the same.
            Run(session, "U");
            Assert.Equal("(Lisp Expression) GROUP", Said(session));
            Assert.Equal("2", Count(session));

            Run(session, "U");
            Assert.Equal("GH9A GROUP", Said(session));
            Assert.Equal("1", Count(session));
        }

        [Fact]
        public void UAfterARoutineOfOneCommand_NamesTheRoutine()
        {
            CadCommandSession session = Start();
            Run(session, "GH9D");

            Run(session, "U");

            // AutoCAD 2021 (l9 M1-b u8): the routine's one (command) is the step "GH9D GROUP"; ours (pin) wrote CIRCLE.
            Assert.Equal("GH9D GROUP", Said(session));
            Assert.Equal("1", Count(session));
        }

        [Theory]
        [InlineData("U")]
        [InlineData("UNDO")]
        public void UAfterATypedEntmod_UndoesItWithTheStepBefore(string typed)
        {
            CadCommandSession session = Start();
            session.Feed("(entmake '((0 . \"LINE\") (10 0.0 60.0 0.0) (11 10.0 60.0 0.0)))");
            session.Feed("(entmod (subst (cons 10 '(1.0 61.0 0.0)) (assoc 10 (entget (entlast))) (entget (entlast))))");

            UndoOnce(session, typed);

            // AutoCAD 2021 (l9 M1-b u5, u5b): an entmake and an entmod typed after a command are one step with it: U or UNDO 1 writes
            // LINE and leaves no object. Green on the pin.
            Assert.Equal("LINE", Said(session));
            Assert.Equal("0", Count(session));
        }

        [Fact]
        public void ACommandUInsideARoutine_WritesItsEchoAndTheRoutineGroupOnOneLine()
        {
            CadCommandSession session = Start();
            Run(session, "GH9C");

            // AutoCAD 2021 (l9 M1-b u6): (command "_.U") in a routine whose first (command ...) drew a line writes
            // `Command: _.U GH9C GROUP` on one line and undoes the routine's line (one object, the start line, is left). Ours
            // (pin) wrote `Command: _.U` and then LINE.
            Assert.Equal("Command: _.U GH9C GROUP", Said(session));
            Assert.Equal("1", Count(session));
        }

        [Fact]
        public void UndoOneAfterARoutineThatRanU_NamesTheLineBeforeIt()
        {
            CadCommandSession session = Start();
            Run(session, "GH9C");

            Run(session, "UNDO", "1");

            // AutoCAD 2021 (l9 M1-b u6b): after the routine undid its own line, UNDO 1 undoes the start line and writes LINE. Green on
            // the pin.
            Assert.Equal("LINE", Said(session));
            Assert.Equal("0", Count(session));
        }

        [Fact]
        public void ATypedCommandUFormTwice_WritesItsEchoAndTheNameOnOneLineEachTime()
        {
            CadCommandSession session = Start();
            Run(session, "_.LINE", "0,5", "10,5", Enter);
            Run(session, "_.CIRCLE", "5,5", "3");

            // AutoCAD 2021 (l9 M1-b u7): (command "_.U") typed twice writes `_.U CIRCLE` and then `_.U LINE`, each on one line.
            // Ours (pin) wrote `_.U` and the name on the next line.
            Assert.Equal("_.U CIRCLE", FirstLineOf(session, "(command \"_.U\")"));
            Assert.Equal("_.U LINE", FirstLineOf(session, "(command \"_.U\")"));
        }

        [Fact]
        public void RedoAfterURoutineGroup_NamesTheGroupWithGroupFirst()
        {
            CadCommandSession session = Start();
            Run(session, "GH9A");
            Run(session, "U");

            Run(session, "REDO");

            // AutoCAD 2021 (l9 M1-b r2 b): REDO right after the U of a routine's circle writes `GROUP (Lisp Expression)` and then
            // `Everything has been redone`; ours (pin) wrote CIRCLE.
            Assert.Equal("GROUP (Lisp Expression)", session.History[session.History.Count - 2].TrimEnd());
            Assert.Equal("Everything has been redone", Said(session));
        }

        [Fact]
        public void RedoAfterULine_NamesTheLine_AndAPrincBetweenDoesNotStopIt()
        {
            CadCommandSession session = Start();
            Run(session, "_.LINE", "0,7", "10,7", Enter);
            Run(session, "U");
            session.Feed("(princ)");

            Run(session, "REDO");

            // AutoCAD 2021 (l9 M1-b r3 f): U, a typed (princ), REDO writes LINE and `Everything has been redone`. Green on the pin.
            Assert.Equal("LINE", session.History[session.History.Count - 2].TrimEnd());
            Assert.Equal("Everything has been redone", Said(session));
        }
    }
}
