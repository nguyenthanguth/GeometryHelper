using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>autoload</c>, which AutoCAD 2021 defines in its <c>acad2021doc.lsp</c>, and <c>findtrustedfile</c> and the screen
    /// functions <c>textscr</c>, <c>textpage</c> and <c>graphscr</c> (probes g7, g9 and g13 of the C4a measurements; the autoload
    /// rows with the file loaded into the accoreconsole, which does not load it by itself).
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispAutoloadTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-c4a-" + Guid.NewGuid().ToString("N"));

        public LispAutoloadTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            WaitingRoutines.EndAll();
            Directory.Delete(_folder, true);
        }

        private LispInterpreter Interpreter()
        {
            string file1 = Path.Combine(_folder, "zzauto.lsp");
            File.WriteAllText(
                file1,
                "(princ \"\\n[zzauto loaded]\")\n(defun c:zzauto1 () (princ \"\\n[zzauto1 runs]\") (princ 42))\n(defun c:zzauto2 (/ a) (setq a 7) a)\n(princ)\n"
            );
            File.WriteAllText(
                Path.Combine(_folder, "zzerr.lsp"),
                "(defun c:zzerr1 () (princ \"\\n[zzerr1 runs]\") (/ 1 0))\n"
            );
            File.WriteAllText(
                Path.Combine(_folder, "zzbad.lsp"),
                "(princ \"\\n[zzbad loaded]\")\n(setq zzbad (/ 1 0))\n(defun c:zzbad1 () 1)\n"
            );
            return new LispInterpreter { SupportFolders = new[] { _folder } };
        }

        private static string Run(LispInterpreter lisp, string form) => lisp.Run(form).Output;

        #region autoload

        [Fact]
        public void Autoload_DefinesTheCommandsAndGivesNil_TheFirstRunLoadsTheFileAndRunsTheCommand()
        {
            // AutoCAD 2021 (g9): (autoload "zzauto" '("zzauto1" "zzauto2")) nil; (c:zzauto1) writes Initializing..., the file's
            // text, the command's text, and 4242 (the princ and the value); the second call is the command itself.
            LispInterpreter lisp = Interpreter();

            Assert.Equal("nil", Run(lisp, "(autoload \"zzauto\" '(\"zzauto1\" \"zzauto2\"))"));
            Assert.True(lisp.IsCommand("zzauto1"));
            Assert.Equal("SUBR", Run(lisp, "(type c:zzauto1)"));
            Assert.Equal("\nInitializing...\n[zzauto loaded]\n[zzauto1 runs]4242", Run(lisp, "(c:zzauto1)"));
            Assert.Equal("\n[zzauto1 runs]4242", Run(lisp, "(c:zzauto1)"));
            Assert.Equal("7", Run(lisp, "(c:zzauto2)"));
        }

        [Fact]
        public void AnErrorInTheCommandOfTheFirstRun_ShowsItsMessageAlone_AndNotTheNextTime()
        {
            // AutoCAD 2021 (g9): the first (c:zzerr1) ends with the message divide by zero, with no ; error: before it, as the
            // stub's handler *merrmsg* writes it; the second is the plain ; error: divide by zero.
            LispInterpreter lisp = Interpreter();
            Run(lisp, "(autoload \"zzerr\" '(\"zzerr1\"))");

            Assert.Equal("\nInitializing...\n[zzerr1 runs]divide by zero", Run(lisp, "(c:zzerr1)"));
            Assert.Equal(
                "\n[zzerr1 runs]; error: divide by zero".TrimStart('\n'),
                Run(lisp, "(c:zzerr1)").TrimStart('\n')
            );
        }

        [Fact]
        public void AnErrorWhileLoading_ShowsItsMessage_AndTheStubStays()
        {
            // AutoCAD 2021 (g9): (c:zzbad1) writes Initializing..., [zzbad loaded]divide by zero; (type c:zzbad1) is still SUBR
            LispInterpreter lisp = Interpreter();
            Run(lisp, "(autoload \"zzbad\" '(\"zzbad1\"))");

            Assert.Equal("\nInitializing...\n[zzbad loaded]divide by zero", Run(lisp, "(c:zzbad1)"));
            Assert.Equal("SUBR", Run(lisp, "(type c:zzbad1)"));
            Assert.Equal("nil", Run(lisp, "(boundp 'zzbad)"));
        }

        [Fact]
        public void AFileNotOnTheSearchPath_WritesAutoCadsTwoLinesAndTheCommandGivesNil()
        {
            // AutoCAD 2021 (g9): The file NAME(.lsp/.exe/.arx) was not found in your search path folders. / Check the installation
            // of the support files and try again., then nil.
            LispInterpreter lisp = Interpreter();
            Run(lisp, "(autoload \"zznofile\" '(\"zznofile1\"))");

            Assert.Equal(
                "\nThe file zznofile(.lsp/.exe/.arx) was not found in your search path folders.\nCheck the installation of the support files and try again.nil",
                Run(lisp, "(c:zznofile1)")
            );
        }

        [Fact]
        public void ACommandThatIsDefinedAlready_IsLeftAlone()
        {
            LispInterpreter lisp = Interpreter();
            Run(lisp, "(defun c:zzauto1 () 1)");
            Run(lisp, "(autoload \"zzauto\" '(\"zzauto1\"))");

            Assert.Equal("1", Run(lisp, "(c:zzauto1)"));
        }

        [Fact]
        public void AFileThatDoesNotDefineTheCommand_RunsTheStubUntilTheStackLimit()
        {
            // AutoCAD 2021 (g9): the stub loads the file and calls itself again, which is the stack limit, 19 991 times over.
            LispInterpreter lisp = Interpreter();
            Run(lisp, "(autoload \"zzauto\" '(\"zzother\"))");

            string output = Run(lisp, "(c:zzother)");

            Assert.EndsWith("Hard error occurred ***\ninternal stack limit reached (simulated)", output);
            Assert.True(output.Split(new[] { "[zzauto loaded]" }, StringSplitOptions.None).Length > 1000);
        }

        [Theory]
        [InlineData("(autoload \"x\" \"zzauto3\")", "; error: bad argument type: listp \"zzauto3\"")] // AutoCAD 2021 (g9)
        [InlineData("(autoload \"x\" '(5))", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g9)
        [InlineData("(autoload 5 '(\"a\"))", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g9)
        [InlineData("(autoload \"x\")", "; error: too few arguments")] // AutoCAD 2021 (g9)
        [InlineData("(autoload)", "; error: too few arguments")] // AutoCAD 2021 (g9)
        [InlineData("(autoload \"x\" '(\"a\") 1)", "; error: too many arguments")] // AutoCAD 2021 (g9)
        [InlineData("(autoload \"x\" nil)", "nil")] // AutoCAD 2021 (g9)
        public void WrongArguments_GiveAutoCadsErrors(string form, string output) =>
            Assert.Equal(output, Run(Interpreter(), form));

        #endregion

        #region findtrustedfile

        [Fact]
        public void FindTrustedFile_GivesTheFileOnlyInAFolderThatIsTrusted()
        {
            // AutoCAD 2021 (g13): findfile finds a file anywhere on its path; findtrustedfile only in a trusted folder (the
            // program's, TRUSTEDPATHS), nil elsewhere and for a folder; SECURELOAD 0 trusts every folder.
            string trusted = Path.Combine(_folder, "trusted");
            string other = Path.Combine(_folder, "other");
            Directory.CreateDirectory(trusted);
            Directory.CreateDirectory(other);
            string inTrusted = Path.Combine(trusted, "a.lsp");
            string inOther = Path.Combine(other, "b.lsp");
            File.WriteAllText(inTrusted, "(princ)");
            File.WriteAllText(inOther, "(princ)");
            CadCommandSession session = Session(Blank());
            session.LoadSecurity = new CadLoadSecurity { SecureLoad = 1, TrustedPaths = trusted };
            LispInterpreter lisp = session.Lisp;

            Assert.Equal(Quoted(inTrusted), Run(lisp, "(findtrustedfile " + Quoted(inTrusted) + ")"));
            Assert.Equal(Quoted(inOther), Run(lisp, "(findfile " + Quoted(inOther) + ")"));
            Assert.Equal("nil", Run(lisp, "(findtrustedfile " + Quoted(inOther) + ")"));
            Assert.Equal("nil", Run(lisp, "(findtrustedfile " + Quoted(trusted) + ")"));
            Assert.Equal("nil", Run(lisp, "(findtrustedfile " + Quoted(Path.Combine(trusted, "a")) + ")"));
            Assert.Equal("nil", Run(lisp, "(findtrustedfile " + Quoted(Path.Combine(trusted, "none.lsp")) + ")"));

            session.LoadSecurity.SecureLoad = 2;
            Assert.Equal("nil", Run(lisp, "(findtrustedfile " + Quoted(inOther) + ")"));
            Assert.Equal(Quoted(inTrusted), Run(lisp, "(findtrustedfile " + Quoted(inTrusted) + ")"));

            session.LoadSecurity.SecureLoad = 0;
            Assert.Equal(Quoted(inOther), Run(lisp, "(findtrustedfile " + Quoted(inOther) + ")"));
        }

        [Theory]
        [InlineData("(findtrustedfile 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g13)
        [InlineData("(findtrustedfile nil)", "; error: bad argument type: stringp nil")] // AutoCAD 2021 (g13)
        [InlineData("(findtrustedfile)", "; error: too few arguments")] // AutoCAD 2021 (g13)
        [InlineData("(findtrustedfile \"a\" \"b\")", "; error: too many arguments")] // AutoCAD 2021 (g13)
        [InlineData("(findtrustedfile \"\")", "nil")] // AutoCAD 2021 (g13)
        [InlineData("(findtrustedfile \"zz-no-such-file.lsp\")", "nil")] // AutoCAD 2021 (g13)
        public void FindTrustedFile_WrongArgumentsAndMissingFiles_AnswerAsAutoCadDoes(string form, string output) =>
            Assert.Equal(output, Run(new LispInterpreter(), form));

        private static string Quoted(string path) => "\"" + path.Replace("\\", "\\\\") + "\"";

        #endregion

        #region textscr, textpage and graphscr

        [Fact]
        public void TheScreenFunctions_AskTheWindowAndGiveNil()
        {
            // AutoCAD 2021 (g7): (textscr), (graphscr) and (textpage) give nil and take no argument; here each asks the window,
            // textpage for the text window as textscr does.
            CadCommandSession session = Session(Blank());
            var asked = new List<string>();
            session.UiRequested += (s, e) =>
            {
                asked.Add(e.Name);
                e.Handled = true;
            };

            Assert.Equal("nil", session.Lisp.Run("(textscr)").Output);
            Assert.Equal("nil", session.Lisp.Run("(textpage)").Output);
            Assert.Equal("nil", session.Lisp.Run("(graphscr)").Output);
            Assert.Equal(new[] { "TEXTSCR", "TEXTSCR", "GRAPHSCR" }, asked);
            Assert.Equal("; error: too many arguments", session.Lisp.Run("(textscr 1)").Output);
            Assert.Equal("; error: too many arguments", session.Lisp.Run("(graphscr 1)").Output);
            Assert.Equal("; error: too many arguments", session.Lisp.Run("(textpage 1)").Output);
        }

        [Fact]
        public void WithNoWindow_TheScreenFunctionsGiveNilAndWriteNothing()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", session.Lisp.Run("(textscr)").Output);
            Assert.Equal("nil", session.Lisp.Run("(graphscr)").Output);
            Assert.Equal("nil", session.Lisp.Run("(textpage)").Output);
            Assert.Empty(session.History);
        }

        #endregion
    }
}
