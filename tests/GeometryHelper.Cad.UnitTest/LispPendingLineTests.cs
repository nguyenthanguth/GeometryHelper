using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The line a routine leaves open: text written with <c>princ</c> and no new line after it is continued, not broken, by what
    /// comes next on the console, as AutoCAD 2021 writes it (L8 acceptance, feeds gk and fx, probe m1c and the routine EE of
    /// L2): a command's echo, a key echoed by a <c>grread</c> loop, the answer to a get function's prompt; and the prompt a
    /// command left waiting by a routine is written again where the routine returns. Each history is compared line by line.
    /// </summary>
    public sealed class LispPendingLineTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        // The history without the trailing spaces of each line, from the line that echoes the typed command.
        private static string[] From(CadCommandSession session, string echo)
        {
            string[] lines = session.History.Select(l => l.TrimEnd()).ToArray();
            return lines.Skip(Array.IndexOf(lines, echo)).ToArray();
        }

        [Fact]
        public void ATextWithNoNewLineBeforeACommand_IsContinuedByTheEchoOfTheCommand()
        {
            // AutoCAD 2021 (probe m1c): (princ "A") then (command "_.LINE" ...) is one line, A_.LINE; the text after the
            // command is written after the Command prompt, as it was.
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:r1 () (princ \"A\") (command \"_.LINE\" \"0,0\" \"1,1\" \"\") (princ \"B\") (princ))",
                "R1"
            );

            Assert.Equal(
                new[]
                {
                    "Command: R1",
                    "A_.LINE",
                    "Specify first point: 0,0",
                    "Specify next point or [Undo]: 1,1",
                    "Specify next point or [Undo]:",
                    "Command: B",
                },
                From(session, "Command: R1")
            );
        }

        [Fact]
        public void ATextWithNoNewLineAroundACommandWithCmdecho0_IsOneLine()
        {
            // ours: CMDECHO 0 writes no echo, so the command adds nothing to the line and the next text goes on it
            CadCommandSession session = Run(
                Session(Blank()),
                "(setvar \"CMDECHO\" 0)",
                "(defun c:r4 () (princ \"A\") (command \"_.LINE\" \"0,0\" \"1,1\" \"\") (princ \"B\") (princ))",
                "R4"
            );

            Assert.Equal(new[] { "Command: R4", "AB" }, From(session, "Command: R4"));
        }

        [Fact]
        public void TheKeysOfAGrreadLoopEchoedWithPrinc_ContinueTheLineOfItsPrompt()
        {
            // AutoCAD 2021 (L8 feed gk): the line is Type: abc, not Type: and a, b, c under it; the blank line is the \n that
            // starts the text, written under the echo as AutoCAD writes it
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:gk ( / g) (princ \"\nType: \") (while (not (member (cadr (setq g (grread))) '(10 13))) (princ (chr (cadr g)))) (princ \"\nDone\") (princ))",
                "GK"
            );

            Assert.True(session.IsRunning);

            foreach (char key in "abc")
            {
                Assert.True(session.FeedKey(key));
            }

            session.FeedKey(13);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "Command: GK", "", "Type: abc", "Done" }, From(session, "Command: GK"));
        }

        [Fact]
        public void ATextWithNoNewLineBeforeAGetFunction_IsContinuedByItsPromptAndTheAnswer()
        {
            // AutoCAD 2021: (princ "A") then (getstring "q: ") typed x is Aq: x; ours: the text after the answer is a line of its own
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:r3 () (princ \"A\") (getstring \"q: \") (princ \"B\") (princ))",
                "R3",
                "x"
            );

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "Command: R3", "Aq: x", "B" }, From(session, "Command: R3"));
        }

        [Fact]
        public void ATextWithNoNewLineBeforeAGetFunctionThatEscIsPressedAt_IsContinuedByItsPromptAndTheCancel()
        {
            // ours: Esc is the answer written after the prompt, so it goes on the same line
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:r5 () (princ \"A\") (getstring \"q: \") (princ))",
                "R5",
                Esc
            );

            Assert.Equal(
                new[] { "Command: R5", "Aq: *Cancel*", "; error: Function cancelled" },
                From(session, "Command: R5")
            );
        }

        [Fact]
        public void TwoWritesWithNoNewLineBetweenThem_StayOnOneLine()
        {
            // AutoCAD 2021 (feed fx): the text of the routine's last princ is the line the next command goes on
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:r6 () (princ \"A\") (princ \"B\") (command \"_.CIRCLE\" \"0,0\" 5) (princ))",
                "R6"
            );

            Assert.Equal(
                new[]
                {
                    "Command: R6",
                    "AB_.CIRCLE",
                    "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]: 0,0",
                    "Specify radius of circle or [Diameter]: 5",
                },
                From(session, "Command: R6")
            );
        }

        [Fact]
        public void ARoutineThatReturnsWhileUcsWaits_WritesThePromptAgainAsTheLineEscCancels()
        {
            // AutoCAD 2021 (routine EE, D1451): the prompt is written when UCS asks it and again where the routine returns;
            // Esc then ends the second
            CadCommandSession session = Run(Session(Blank()), "(defun c:ee () (command \"ucs\" \"e\") (princ))", "EE");

            Assert.True(session.IsRunning);
            Assert.Equal(
                new[]
                {
                    "Command: EE",
                    "ucs",
                    "Current ucs name:  *WORLD*",
                    "Specify origin of UCS or [Face/NAmed/OBject/Previous/View/World/X/Y/Z/ZAxis] <World>: e",
                    "Select object to align UCS:",
                },
                From(session, "Command: EE")
            );

            Run(session, Esc);

            Assert.Equal(
                new[]
                {
                    "Command: EE",
                    "ucs",
                    "Current ucs name:  *WORLD*",
                    "Specify origin of UCS or [Face/NAmed/OBject/Previous/View/World/X/Y/Z/ZAxis] <World>: e",
                    "Select object to align UCS:",
                    "Select object to align UCS: *Cancel*",
                },
                From(session, "Command: EE")
            );
        }
    }
}
