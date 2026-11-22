using System;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>*push-error-using-command*</c>, <c>*push-error-using-stack*</c>, <c>*pop-error-mode*</c> and what they decide: whether
    /// <c>command</c> may be called in <c>*error*</c>, against AutoCAD 2021's accoreconsole (probes g5b, g5c and g5d of the
    /// C4a measurements). <c>command-s</c> is always allowed there.
    /// </summary>
    public sealed class LispErrorModeTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private const string Refusal =
            "Cannot invoke (command) from *error* without prior call to (*push-error-using-command*).\n"
            + "Converting (command) calls to (command-s) is recommended.";

        private static string Out(LispInterpreter lisp, string form) => lisp.Run(form).Output;

        private static int Points(CadCommandSession session) => All<DxfDBPoint>(session.Document.ModelSpace).Count();

        private static CadCommandSession Defined(params string[] forms)
        {
            CadCommandSession session = Session(Blank());
            session.Feed("(setvar \"CMDECHO\" 0)");

            foreach (string form in forms)
            {
                session.Feed(form);
            }

            return session;
        }

        // The text one typed form wrote, without the form itself.
        private static string Written(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join(
                "\n",
                session.History.Skip(before + 1).Select(l => l.TrimEnd()).Where(l => l.Length > 0)
            );
        }

        #region The three functions

        [Fact]
        public void ThePushFunctionsAndThePop_AnswerTWhereAModeWasPushed()
        {
            // AutoCAD 2021 (g5c): (list (*push-error-using-command*) (*pop-error-mode*) (*push-error-using-stack*) (*pop-error-mode*))
            // is (T T T T); the same forms alone, on one typed line each, give T, then the underflow.
            var lisp = new LispInterpreter();

            Assert.Equal(
                "(T T T T)",
                Out(
                    lisp,
                    "(list (*push-error-using-command*) (*pop-error-mode*) (*push-error-using-stack*) (*pop-error-mode*))"
                )
            );
            Assert.Equal("T", Out(lisp, "(*push-error-using-command*)"));
            Assert.Equal("T", Out(lisp, "(*push-error-using-stack*)"));
        }

        [Fact]
        public void APopWithNothingPushed_IsAnUnderflowThatVlCatchAllApplyCatches()
        {
            // AutoCAD 2021 (g5b, g5c): ; error: (*pop-error-mode*) underflow. , also when the mode was pushed on a line before: a
            // mode lasts to the end of the evaluation that pushed it.
            var lisp = new LispInterpreter();

            Assert.Equal("; error: (*pop-error-mode*) underflow.", Out(lisp, "(*pop-error-mode*)"));
            Out(lisp, "(*push-error-using-command*)");
            Assert.Equal("; error: (*pop-error-mode*) underflow.", Out(lisp, "(*pop-error-mode*)"));
            Assert.Equal(
                "\"(*pop-error-mode*) underflow.\"",
                Out(lisp, "(vl-catch-all-error-message (vl-catch-all-apply '*pop-error-mode* nil))")
            );
            Assert.Equal(
                "; error: (*pop-error-mode*) underflow.",
                Out(
                    lisp,
                    "(progn (*push-error-using-stack*) (*push-error-using-stack*) (*pop-error-mode*) (*pop-error-mode*) (*pop-error-mode*))"
                )
            );
        }

        [Theory]
        [InlineData("(*push-error-using-command* 1)")] // AutoCAD 2021 (g5): ; error: too many arguments
        [InlineData("(*push-error-using-stack* 1)")] // AutoCAD 2021 (g5)
        [InlineData("(*pop-error-mode* 1)")] // AutoCAD 2021 (g5)
        public void TheErrorModeFunctionsTakeNoArguments(string form) =>
            Assert.Equal("; error: too many arguments", Out(new LispInterpreter(), form));

        #endregion

        #region command in *error*

        [Fact]
        public void CommandInErrorWithNoPush_IsRefusedWithAutoCadsTwoLinesAndTheHandlerStops()
        {
            // AutoCAD 2021 (g5b, E1): (c:ZT1) writes E1:divide by zero, the two lines, and no "E1 done"; no point is made.
            CadCommandSession session = Defined(
                "(defun c:ZT1 () (/ 1 0))",
                "(defun *error* (m) (princ (strcat \"\\nE1:\" m)) (command \"_.POINT\" \"0,0\") (princ \"\\nE1 done\") (princ))"
            );

            Assert.Equal("E1:divide by zero\n" + Refusal, Written(session, "(c:ZT1)").TrimStart('\n'));
            Assert.Equal(0, Points(session));
        }

        [Fact]
        public void CommandInErrorAfterPushUsingCommand_Works()
        {
            // AutoCAD 2021 (g5b, E5): the push in the command, before the error: the handler's command ran and "E5 done" is written.
            CadCommandSession session = Defined(
                "(defun zerr (m) (princ (strcat \"\\nE5:\" m)) (command \"_.POINT\" \"4,0\") (princ \"\\nE5 done\") (princ))",
                "(defun c:ZT2 () (*push-error-using-command*) (setq *error* zerr) (/ 1 0))"
            );

            Assert.Equal("E5:divide by zero\nE5 done", Written(session, "(c:ZT2)").TrimStart('\n'));
            Assert.Equal(1, Points(session));
        }

        [Fact]
        public void APushInsideTheHandlerIsTooLate()
        {
            // AutoCAD 2021 (g5b, E3): (*push-error-using-command*) inside *error* itself does not allow its command.
            CadCommandSession session = Defined(
                "(defun c:ZT1 () (/ 1 0))",
                "(defun *error* (m) (princ (strcat \"\\nE3:\" m)) (*push-error-using-command*) (command \"_.POINT\" \"2,0\") (*pop-error-mode*) (princ \"\\nE3 done\") (princ))"
            );

            Assert.Equal("E3:divide by zero\n" + Refusal, Written(session, "(c:ZT1)").TrimStart('\n'));
            Assert.Equal(0, Points(session));
        }

        [Theory]
        [InlineData("(*push-error-using-stack*)")] // AutoCAD 2021 (g5b, E7): the stack mode does not allow command
        [InlineData("(*push-error-using-command*) (*push-error-using-stack*)")] // AutoCAD 2021 (g5c, ZT7): the innermost mode counts
        [InlineData("(*push-error-using-command*) (*pop-error-mode*)")] // AutoCAD 2021 (g5c, ZT6): a mode popped does not
        public void CommandInErrorIsRefusedWhereTheInnermostModeIsNotUsingCommand(string pushes)
        {
            CadCommandSession session = Defined(
                "(defun zerr (m) (princ (strcat \"\\nE5:\" m)) (command \"_.POINT\" \"4,0\") (princ \"\\nE5 done\") (princ))",
                "(defun c:ZT () " + pushes + " (setq *error* zerr) (/ 1 0))"
            );

            Assert.Equal("E5:divide by zero\n" + Refusal, Written(session, "(c:ZT)").TrimStart('\n'));
            Assert.Equal(0, Points(session));
        }

        [Fact]
        public void CommandInErrorIsAllowedAgainAfterTheStackModeIsPopped()
        {
            // AutoCAD 2021 (g5c, ZT8): command, stack, pop leaves command innermost: the handler's command runs.
            CadCommandSession session = Defined(
                "(defun zerr (m) (princ (strcat \"\\nE5:\" m)) (command \"_.POINT\" \"4,0\") (princ \"\\nE5 done\") (princ))",
                "(defun c:ZT () (*push-error-using-command*) (*push-error-using-stack*) (*pop-error-mode*) (setq *error* zerr) (/ 1 0))"
            );

            Assert.Equal("E5:divide by zero\nE5 done", Written(session, "(c:ZT)").TrimStart('\n'));
            Assert.Equal(1, Points(session));
        }

        [Theory]
        [InlineData("", "E2")] // AutoCAD 2021 (g5b, E2): command-s with no push at all
        [InlineData("(*push-error-using-stack*)", "E6")] // AutoCAD 2021 (g5b, E6)
        [InlineData("(*push-error-using-command*)", "E8")] // AutoCAD 2021 (g5b, E8)
        public void CommandSInErrorIsAlwaysAllowed(string push, string tag)
        {
            CadCommandSession session = Defined(
                "(defun zerr (m) (princ (strcat \"\\n"
                    + tag
                    + ":\" m)) (command-s \"_.POINT\" \"1,0\") (princ \"\\n"
                    + tag
                    + " done\") (princ))",
                "(defun c:ZT () " + push + " (setq *error* zerr) (/ 1 0))"
            );

            Assert.Equal(tag + ":divide by zero\n" + tag + " done", Written(session, "(c:ZT)").TrimStart('\n'));
            Assert.Equal(1, Points(session));
        }

        [Fact]
        public void VlCmdfInErrorIsRefusedLikeCommand_AndCommandWithNoArgumentsToo()
        {
            // AutoCAD 2021 (g5c, ZT11 and ZT13): the same two lines; and with the push (ZT12) it runs.
            CadCommandSession session = Defined(
                "(defun zerr11 (m) (princ (strcat \"\\nE11:\" m)) (vl-cmdf \"_.POINT\" \"8,0\") (princ \"\\nE11 done\") (princ))",
                "(defun c:ZT11 () (setq *error* zerr11) (/ 1 0))",
                "(defun c:ZT12 () (*push-error-using-command*) (setq *error* zerr11) (/ 1 0))",
                "(defun zerr13 (m) (princ (strcat \"\\nE13:\" m)) (list (command)) (princ \"\\nE13 done\") (princ))",
                "(defun c:ZT13 () (setq *error* zerr13) (/ 1 0))"
            );

            Assert.Equal("E11:divide by zero\n" + Refusal, Written(session, "(c:ZT11)").TrimStart('\n'));
            Assert.Equal("E11:divide by zero\nE11 done", Written(session, "(c:ZT12)").TrimStart('\n'));
            Assert.Equal("E13:divide by zero\n" + Refusal, Written(session, "(c:ZT13)").TrimStart('\n'));
            Assert.Equal(1, Points(session));
        }

        [Fact]
        public void TheHandlerCalledByHandIsNotInTheErrorMode()
        {
            // AutoCAD 2021 (g5c, ZT10 and the typed (*error* "typed")): called by hand it runs its command.
            CadCommandSession session = Defined(
                "(defun zerr (m) (princ (strcat \"\\nE5:\" m)) (command \"_.POINT\" \"4,0\") (princ \"\\nE5 done\") (princ))",
                "(setq *error* zerr)"
            );

            Assert.Equal("E5:typed\nE5 done", Written(session, "(*error* \"typed\")").TrimStart('\n'));
            Assert.Equal(1, Points(session));
        }

        [Fact]
        public void WithNoHandlerAndTheCommandModePushed_TheMessageIsLost()
        {
            // AutoCAD 2021 (g5d, ZT15): (setq *error* nil), then a routine that pushes using-command and fails prints
            // "INTERNAL error in FAIL\nmessage lost, reset to top" and no ; error: line; with the stack mode, or with the mode
            // popped, the line is the usual one.
            var lisp = new LispInterpreter();
            lisp.Run("(defun c:ZT15 () (*push-error-using-command*) (/ 1 0))");
            lisp.Run("(defun c:ZT15d () (*push-error-using-stack*) (/ 1 0))");
            lisp.Run("(defun c:ZT15f () (*push-error-using-command*) (*pop-error-mode*) (/ 1 0))");

            Assert.Equal("\"INTERNAL error in FAIL\\nmessage lost, reset to top\"", lisp.RunCommand("ZT15").Output);
            Assert.Equal("; error: divide by zero", lisp.RunCommand("ZT15d").Output);
            Assert.Equal("; error: divide by zero", lisp.RunCommand("ZT15f").Output);
            Assert.Equal("; error: divide by zero", Out(lisp, "(/ 1 0)"));
        }

        #endregion
    }
}
