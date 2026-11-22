using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The functions of the menus and help, the default <c>*error*</c> and the quiet exits, with the values AutoCAD 2021's
    /// accoreconsole gave for each call (the L8 measurements m/func: f1, f2 and f3). The console has no menu bar, so every
    /// menu string but a DIESEL one gave nil there.
    /// </summary>
    public sealed class LispL8FunctionTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Fresh() => Session(Blank());

        private static string V(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        #region *error*

        [Fact]
        public void AFreshSessionsErrorFunction_IsASubrThatDoesNothingWhenCalledByHand()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("SUBR", V(session, "(type *error*)"));
            Assert.Equal("nil", V(session, "(*error* \"x\")"));
        }

        [Fact]
        public void AnErrorWithTheDefaultErrorFunction_WritesTheErrorLine()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("; error: divide by zero", V(session, "(/ 1 0)"));
        }

        [Fact]
        public void AnErrorFunctionSetToNil_LeavesTheDefaultLineAndTypeNil()
        {
            CadCommandSession session = Fresh();
            V(session, "(setq *error* nil)");

            Assert.Equal("nil", V(session, "(type *error*)"));
            Assert.Equal("; error: divide by zero", V(session, "(/ 1 0)"));
        }

        [Fact]
        public void AUserErrorFunction_IsCalledWithTheMessageAndNoErrorLineIsWritten()
        {
            CadCommandSession session = Fresh();
            V(session, "(defun *error* (m) (princ (strcat \"[*error* got: \" m \"]\")))");

            Assert.Equal("[*error* got: divide by zero]", V(session, "(/ 1 0)"));
        }

        [Fact]
        public void AnErrorCaughtByCatchAllApply_DoesNotCallTheErrorFunction()
        {
            CadCommandSession session = Fresh();
            V(session, "(defun *error* (m) (princ \"CALLED\"))");

            Assert.Equal("T", V(session, "(vl-catch-all-error-p (vl-catch-all-apply '/ '(1 0)))"));
        }

        [Fact]
        public void TheOldHandlerSavedAndPutBack_IsTheDefaultSubrAgain()
        {
            CadCommandSession session = Fresh();
            V(session, "(setq old *error*)");
            V(session, "(defun *error* (m) (princ \"MINE\"))");
            V(session, "(setq *error* old)");

            Assert.Equal("; error: divide by zero", V(session, "(/ 1 0)"));
        }

        #endregion

        #region vl-exit-with-error and vl-exit-with-value

        [Theory]
        [InlineData("vl-exit-with-error \"x\"")]
        [InlineData("vl-exit-with-error 5")]
        [InlineData("vl-exit-with-error nil")]
        [InlineData("vl-exit-with-value 42")]
        public void AQuietExit_EndsTheEvaluationWithNothingWrittenAndNoErrorFunctionCalled(string call)
        {
            CadCommandSession session = Fresh();
            V(session, "(defun *error* (m) (princ \"CALLED\"))");
            V(session, "(defun ve1 () (" + call + ") (princ \"AFTER\"))");

            Assert.Equal(string.Empty, V(session, "(progn (ve1) (princ \"AFTER\"))"));
        }

        [Fact]
        public void AQuietExitUnderCatchAllApply_GivesAnErrorObjectWithNoMessage()
        {
            CadCommandSession session = Fresh();
            V(session, "(defun ve1 () (vl-exit-with-value 42) 7)");
            V(session, "(setq r (vl-catch-all-apply 've1 nil))");

            Assert.Equal("T", V(session, "(vl-catch-all-error-p r)"));
            Assert.Equal("nil", V(session, "(vl-catch-all-error-message r)"));
            Assert.Equal("\"#<%catch-all-apply-error%>\"", V(session, "(vl-princ-to-string r)"));
        }

        [Fact]
        public void AQuietExitInAnErrorFunction_StopsTheRestOfItAndWritesNothingMore()
        {
            CadCommandSession session = Fresh();
            V(session, "(defun *error* (m) (princ \"H\") (vl-exit-with-error \"passed on\") (princ \"NOT REACHED\"))");

            Assert.Equal("H", V(session, "(/ 1 0)"));
        }

        [Fact]
        public void AQuietExitWithTheWrongNumberOfArguments_IsTheUsualError()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("; error: too few arguments", V(session, "(vl-exit-with-error)"));
            Assert.Equal("; error: too many arguments", V(session, "(vl-exit-with-value 1 2)"));
        }

        #endregion

        #region menucmd, menugroup, vmon

        [Theory]
        [InlineData("M=$(+,1,2)", "3")]
        [InlineData("M=$(-,10,3)", "7")]
        [InlineData("M=$(*,6,7)", "42")]
        [InlineData("M=$(/,10,4)", "2.5")]
        [InlineData("M=$(getvar,clayer)", "0")]
        [InlineData("M=$(upper,abc)", "ABC")]
        [InlineData("M=$(strlen,abcd)", "4")]
        [InlineData("M=$(substr,abcdef,2,3)", "bcd")]
        [InlineData("M=abc", "abc")]
        [InlineData("M=", "")]
        public void MenucmdOfAMenuMacro_GivesTheDieselResultAsAString(string text, string result)
        {
            CadCommandSession session = Fresh();

            Assert.Equal("\"" + result + "\"", V(session, "(menucmd \"" + text + "\")"));
        }

        [Theory]
        [InlineData("P1=*")]
        [InlineData("P2=+ACAD.POP2")]
        [InlineData("GACAD.ID_Line=~")]
        [InlineData("nonsense")]
        [InlineData("")]
        public void MenucmdOfAnyOtherString_IsNilAsTheConsoleGivesIt(string text)
        {
            Assert.Equal("nil", V(Fresh(), "(menucmd \"" + text + "\")"));
        }

        [Fact]
        public void MenucmdAndMenugroupWithAnArgumentThatIsNoString_AreTheUsualTypeError()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("; error: bad argument type: stringp 5", V(session, "(menucmd 5)"));
            Assert.Equal("; error: bad argument type: stringp nil", V(session, "(menucmd nil)"));
            Assert.Equal("; error: bad argument type: stringp nil", V(session, "(menugroup nil)"));
        }

        [Theory]
        [InlineData("(menugroup \"ACAD\")")]
        [InlineData("(menugroup \"NONE\")")]
        [InlineData("(vmon)")]
        public void MenugroupAndVmon_GiveNilWithNoMenuGroupsLoaded(string form)
        {
            Assert.Equal("nil", V(Fresh(), form));
        }

        #endregion

        #region help and setfunhelp

        [Fact]
        public void HelpOfAFileThatIsNotThere_GivesItsFirstArgumentAndOpensNothing()
        {
            CadCommandSession session = Fresh();
            var opened = new List<string>();
            session.Lisp.HelpOpener = opened.Add;

            Assert.Equal("\"nosuch.chm\"", V(session, "(help \"nosuch.chm\" \"topic\")"));
            Assert.Empty(opened);
        }

        [Fact]
        public void HelpOfAFileThatIsThere_HandsItsFullPathToTheWindow()
        {
            string file = Path.Combine(Path.GetTempPath(), "gh-help-" + Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllText(file, "x");

            try
            {
                CadCommandSession session = Fresh();
                var opened = new List<string>();
                session.Lisp.HelpOpener = opened.Add;

                V(session, "(help \"" + file.Replace('\\', '/') + "\")");

                Assert.Equal(file, Assert.Single(opened), StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void HelpWithNoWindowToOpenIt_StillGivesItsFirstArgument()
        {
            Assert.Equal("\"\"", V(Fresh(), "(help \"\")"));
        }

        [Fact]
        public void HelpAndSetfunhelpWithAnArgumentThatIsNoString_AreTheUsualTypeError()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("; error: bad argument type: stringp 5", V(session, "(help 5)"));
            Assert.Equal("; error: bad argument type: stringp 5", V(session, "(help \"f.chm\" 5)"));
            Assert.Equal("; error: bad argument type: stringp 1", V(session, "(help \"f.chm\" \"t\" 1)"));
            Assert.Equal(
                "; error: bad argument type: stringp 1",
                V(session, "(setfunhelp \"c:abc\" \"x.hlp\" \"t\" 1)")
            );
            Assert.Equal("; error: bad argument type: stringp ABC", V(session, "(setfunhelp 'abc \"x.hlp\")"));
        }

        [Fact]
        public void Setfunhelp_GivesTheNameOfTheFunction_WhateverTheNumberOfArguments()
        {
            CadCommandSession session = Fresh();

            Assert.Equal("\"c:abc\"", V(session, "(setfunhelp \"c:abc\")"));
            Assert.Equal("\"c:abc\"", V(session, "(setfunhelp \"c:abc\" \"x.hlp\")"));
            Assert.Equal("\"c:abc\"", V(session, "(setfunhelp \"c:abc\" \"x.hlp\" \"t\" \"c\")"));
        }

        #endregion

        #region getcname

        [Theory]
        [InlineData("LINE", "_LINE")]
        [InlineData("line", "_LINE")]
        [InlineData(".LINE", "_LINE")]
        [InlineData("ZOOM", "_ZOOM")]
        [InlineData("-LAYER", "_-LAYER")]
        [InlineData("_LINE", "LINE")]
        [InlineData("_.LINE", "LINE")]
        [InlineData("_-LAYER", "-LAYER")]
        public void GetcnameOfACommandName_GivesTheOtherFormOfItInCapitals(string given, string result)
        {
            Assert.Equal("\"" + result + "\"", V(Fresh(), "(getcname \"" + given + "\")"));
        }

        [Theory]
        [InlineData("NOSUCH")]
        [InlineData("L")]
        [InlineData("")]
        public void GetcnameOfAnAliasOrANameNoCommandHas_IsNil(string given)
        {
            Assert.Equal("nil", V(Fresh(), "(getcname \"" + given + "\")"));
        }

        [Fact]
        public void GetcnameOfALispCommand_IsNil()
        {
            CadCommandSession session = Fresh();
            V(session, "(defun c:abcx () (princ))");

            Assert.Equal("nil", V(session, "(getcname \"ABCX\")"));
            Assert.Equal("nil", V(session, "(getcname \"C:ABCX\")"));
        }

        #endregion

        #region fnsplitl

        [Theory]
        [InlineData("c:/a/b.lsp", "(\"C:\\\\a\\\\\" \"b\" \".lsp\")")]
        [InlineData("c:\\a\\b.lsp", "(\"C:\\\\a\\\\\" \"b\" \".lsp\")")]
        [InlineData("c:/a/b.c.d", "(\"C:\\\\a\\\\\" \"b.c\" \".d\")")]
        [InlineData("c:/a.b/c", "(\"C:\\\\a.b\\\\\" \"c\" \"\")")]
        [InlineData("c:/a/b.lsp ", "(\"C:\\\\a\\\\\" \"b\" \".lsp\")")]
        [InlineData("b", "(\"\" \"b\" \"\")")]
        [InlineData("b.", "(\"\" \"b\" \".\")")]
        [InlineData("c:b.lsp", "(\"C:\\\\\" \"b\" \".lsp\")")]
        public void FnsplitlOfAFileName_GivesItsFullFolderNameAndExtension(string given, string result)
        {
            Assert.Equal(result, V(Fresh(), "(fnsplitl \"" + given.Replace("\\", "\\\\") + "\")"));
        }

        [Theory]
        [InlineData("c:\\\\a\\\\")]
        [InlineData(".lsp")]
        [InlineData("")]
        [InlineData("c:")]
        public void FnsplitlOfAFolderOrNoNameOrADriveAlone_IsNil(string given)
        {
            Assert.Equal("nil", V(Fresh(), "(fnsplitl \"" + given + "\")"));
        }

        [Fact]
        public void FnsplitlOfARelativeName_StartsFromTheCurrentFolderOfTheSession()
        {
            CadCommandSession session = Fresh();
            string folder = Path.GetFullPath(session.Lisp.CurrentFolder());
            string expected = (folder.TrimEnd('\\') + "\\x\\").Replace("\\", "\\\\");

            Assert.Equal(
                "(\"" + char.ToUpperInvariant(expected[0]) + expected.Substring(1) + "\" \"b\" \".lsp\")",
                V(session, "(fnsplitl \"x/b.lsp\")")
            );
        }

        #endregion

        #region acdimenableupdate

        [Fact]
        public void Acdimenableupdate_GivesTheFlagItHadAndSetsItToTForAnythingButNil()
        {
            CadCommandSession session = Fresh();

            // AutoCAD 2021 (f3): T, T (now nil), nil, nil (now T), T, T (0 is not nil), T (now nil), nil, nil
            Assert.Equal("T", V(session, "(acdimenableupdate)"));
            Assert.Equal("T", V(session, "(acdimenableupdate nil)"));
            Assert.Equal("nil", V(session, "(acdimenableupdate)"));
            Assert.Equal("nil", V(session, "(acdimenableupdate T)"));
            Assert.Equal("T", V(session, "(acdimenableupdate)"));
            Assert.Equal("T", V(session, "(acdimenableupdate 0)"));
            Assert.Equal("T", V(session, "(acdimenableupdate nil)"));
            Assert.Equal("nil", V(session, "(acdimenableupdate nil)"));
            Assert.Equal("nil", V(session, "(acdimenableupdate \"x\")"));
        }

        #endregion
    }
}
