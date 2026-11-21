using System;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>(load)</c> against AutoCAD 2021's accoreconsole (probes p2 and p3): a name with a folder is that file, any other is
    /// looked for in the support folders and not in the folder of the drawing, the forms are read and evaluated one at a
    /// time so an error leaves what ran, and the value of the last form comes back. The files are made in a folder of their
    /// own under the temporary folder and removed after.
    /// </summary>
    public sealed class LispLoadTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-load-" + Guid.NewGuid().ToString("N")
        );

        public LispLoadTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private string File(string name, string text, Encoding encoding = null)
        {
            string path = Path.Combine(_folder, name);
            System.IO.File.WriteAllBytes(path, (encoding ?? new UTF8Encoding(false)).GetBytes(text));
            return path;
        }

        private LispInterpreter WithSupport()
        {
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };
            File("ghl1a.lsp", "(defun c:ghl1 () (princ \"L\") (princ))\n42\n");
            return lisp;
        }

        private static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        [Fact]
        public void ANameWithNoFolder_IsFoundInTheSupportFolders_AndGivesTheLastValue()
        {
            // AutoCAD 2021: (load "ghl1a") 42, a file on the support path; (c:ghl1) then runs what it defined
            LispInterpreter lisp = WithSupport();

            Expect(lisp, "(load \"ghl1a\")", "42");
            Assert.True(lisp.IsCommand("ghl1"));
            Expect(lisp, "(c:ghl1)", "L");
        }

        [Fact]
        public void LspIsAddedOnlyToANameWithNoExtension()
        {
            // AutoCAD 2021: (load "ghl1a.lsp") with the file on the path: 42
            LispInterpreter lisp = WithSupport();

            Expect(lisp, "(load \"ghl1a.lsp\")", "42");
            File("plain.txt", "(setq fromtxt 5)");
            Expect(lisp, "(load \"plain.txt\")", "5");
            Expect(lisp, "(load \"plain\")", "; error: LOAD failed: \"plain\"");
        }

        [Fact]
        public void ANameWithAFolder_IsThatFile_WithSlashesOrBackslashes()
        {
            // AutoCAD 2021: (load "D:/.../ghl1a.lsp") 42   (load "D:\\...\\ghl1a") 42: with no support folders at all
            WithSupport();
            var lisp = new LispInterpreter();
            string path = Path.Combine(_folder, "ghl1a");

            Expect(lisp, "(load \"" + path.Replace('\\', '/') + ".lsp\")", "42");
            Expect(lisp, "(load \"" + path.Replace("\\", "\\\\") + "\")", "42");
        }

        [Fact]
        public void ANameNotOnTheSupportPath_IsNotFound_AsTheFolderOfTheDrawingIsNotSearched()
        {
            // AutoCAD 2021: (load "ghl1a") with the file beside the drawing but not on the path: ; error: LOAD failed: "ghl1a"
            // (load "ghl1a.lsp") ; error: LOAD failed: "ghl1a.lsp"; also with SECURELOAD 0
            File("ghl1a.lsp", "42");
            var lisp = new LispInterpreter();

            Expect(lisp, "(load \"ghl1a\")", "; error: LOAD failed: \"ghl1a\"");
            Expect(lisp, "(load \"ghl1a.lsp\")", "; error: LOAD failed: \"ghl1a.lsp\"");
            Expect(lisp, "(c:ghl1)", "; error: no function definition: C:GHL1");
        }

        [Fact]
        public void TheSupportFolders_AreSearchedInOrder()
        {
            string second = Path.Combine(_folder, "second");
            Directory.CreateDirectory(second);
            File("order.lsp", "(setq which 'first)");
            System.IO.File.WriteAllText(Path.Combine(second, "order.lsp"), "(setq which 'second)");
            var lisp = new LispInterpreter { SupportFolders = new[] { second, _folder } };

            lisp.Run("(load \"order\")");

            Assert.Equal("SECOND", LispInterpreter.Print(lisp.GetValue("which"), false));
        }

        [Theory]
        [InlineData("(load \"nosuch\")", "; error: LOAD failed: \"nosuch\"")] // AutoCAD 2021: ; error: LOAD failed: "nosuch"
        [InlineData("(load \"nosuch\" \"fb\")", "\"fb\"")] // AutoCAD 2021: "fb"
        [InlineData("(load \"nosuch\" nil)", "nil")] // AutoCAD 2021: nil
        [InlineData("(load 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021: ; error: bad argument type: stringp 5
        [InlineData("(load)", "; error: too few arguments")] // AutoCAD 2021: ; error: too few arguments
        public void ALoadThatFails_GivesTheErrorOrTheValueOfOnFailure(string expression, string output)
        {
            Assert.Equal(output, WithSupport().Run(expression).Output);
        }

        [Fact]
        public void AnErrorInAFile_EndsTheLoad_AndLeavesWhatRan()
        {
            // AutoCAD 2021: (load ".../ghl1b.lsp") ; error: bad argument type: numberp: "a", then !ghb is 1
            File("ghl1b.lsp", "(setq ghb 1)\n(+ 1 \"a\")\n(setq ghb2 2)\n");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(lisp, "(load \"ghl1b\")", "; error: bad argument type: numberp: \"a\"");
            Assert.Equal(1, lisp.GetValue("ghb"));
            Assert.Null(lisp.GetValue("ghb2"));
        }

        [Fact]
        public void AMalformedTail_IsAnErrorAfterTheFormsBeforeItRan()
        {
            // AutoCAD 2021: (load ".../ghl1d.lsp") ; error: malformed list on input, then !ghd is 1
            File("ghl1d.lsp", "(setq ghd 1)\n(defun broken (\n");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(lisp, "(load \"ghl1d\")", "; error: malformed list on input");
            Assert.Equal(1, lisp.GetValue("ghd"));
        }

        [Fact]
        public void WhatTheFormsOfAFileWrite_IsKept_AndTheValueIsEchoed()
        {
            // AutoCAD 2021: a file of (defun c:ghl1e ...) and "loaded-e" gives "loaded-e" for (load "ghl1e")
            File("ghl1e.lsp", "(defun c:ghl1e () (princ \"E\") (princ))\n(princ \"hello\")\n\"loaded-e\"\n");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(lisp, "(load \"ghl1e\")", "hello\"loaded-e\"");
        }

        [Fact]
        public void AFileThatEndsWithPrinc_EchoesNothing()
        {
            File("quiet.lsp", "(setq q 1)\n(princ)\n");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(lisp, "(load \"quiet\")", "");
        }

        [Fact]
        public void AFileMayLoadAnotherFile()
        {
            File("inner.lsp", "(setq innerdone t)\n7");
            File("outer.lsp", "(setq a (load \"inner\"))\n(* a 2)");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(lisp, "(load \"outer\")", "14");
            Assert.Equal(7, lisp.GetValue("a"));
        }

        [Fact]
        public void ACompiledFile_IsRefused()
        {
            // Not measured (our wording, Part C rule): compiled AutoLISP is not supported
            File("routine.fas", "not lisp");
            File("routine.vlx", "not lisp");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            Expect(
                lisp,
                "(load \"routine.fas\")",
                "; error: LOAD failed: \"routine.fas\" (compiled AutoLISP is not supported)"
            );
            Expect(
                lisp,
                "(load \"routine.vlx\")",
                "; error: LOAD failed: \"routine.vlx\" (compiled AutoLISP is not supported)"
            );
            Expect(lisp, "(load \"routine.fas\" \"fb\")", "\"fb\"");
        }

        [Fact]
        public void TheLoadMethod_DoesWhatTheLoadFunctionDoes()
        {
            LispInterpreter lisp = WithSupport();
            LispResult found = lisp.Load("ghl1a");
            LispResult missing = lisp.Load("nosuch");
            LispResult fallback = lisp.Load("nosuch", "fb");

            Assert.Equal(42, found.Value);
            Assert.False(found.Failed);

            // The method is what APPLOAD and the startup files use: the file's value is given back, not written; APPLOAD
            // writes its own "successfully loaded" line instead.
            Assert.Equal(string.Empty, found.Output);
            Assert.True(missing.Failed);
            Assert.Equal("LOAD failed: \"nosuch\"", missing.Error);
            Assert.Equal("fb", fallback.Value);
            Assert.False(fallback.Failed);
        }

        [Fact]
        public void AFileOfUtf8WithABom_IsReadAsUtf8()
        {
            string path = Path.Combine(_folder, "bom.lsp");
            var bytes = new System.Collections.Generic.List<byte> { 0xEF, 0xBB, 0xBF };
            bytes.AddRange(Encoding.UTF8.GetBytes("(setq s \"\u0111\u00e1\")"));
            System.IO.File.WriteAllBytes(path, bytes.ToArray());
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            lisp.Run("(load \"bom\")");

            Assert.Equal("\u0111\u00e1", lisp.GetValue("s"));
        }

        [Fact]
        public void AFileOfUtf8WithNoBom_IsReadAsUtf8()
        {
            File("utf8.lsp", "(setq s \"\u0111\u00e1\")");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            lisp.Run("(load \"utf8\")");

            Assert.Equal("\u0111\u00e1", lisp.GetValue("s"));
            Assert.Equal("2", lisp.Run("(strlen s)").Output);
        }

        [Fact]
        public void AFileInTheAnsiCodePage_IsReadAsAnsi()
        {
            // The routines of the maintainer include files that are not valid UTF-8: a Windows-1252 byte such as E9 (e acute)
            // must come out as the one character the system's ANSI code page gives for it, not as a replacement character.
            string path = Path.Combine(_folder, "ansi.lsp");
            var bytes = new System.Collections.Generic.List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("; caf"));
            bytes.Add(0xE9);
            bytes.AddRange(Encoding.ASCII.GetBytes("\n(setq s \"caf"));
            bytes.Add(0xE9);
            bytes.AddRange(Encoding.ASCII.GetBytes("\")\n(setq ok 1)"));
            System.IO.File.WriteAllBytes(path, bytes.ToArray());
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            lisp.Run("(load \"ansi\")");

            Assert.Equal(1, lisp.GetValue("ok"));
            string expected = "caf" + Encoding.Default.GetString(new byte[] { 0xE9 });
            Assert.Equal(expected, lisp.GetValue("s"));
            Assert.Equal("4", lisp.Run("(strlen s)").Output);
            Assert.DoesNotContain("\ufffd", (string)lisp.GetValue("s"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFileWithAUnicodeEscapeInAString_ShowsTheCharacterOnTheCommandLine()
        {
            // AutoCAD 2021: (setq g1 "a\U+1EC5b") from a file; (prin1 g1) shows the character, (strlen g1) is 9
            File("uni.lsp", "(setq g1 \"a\\U+1EC5b\")");
            var lisp = new LispInterpreter { SupportFolders = new[] { _folder } };

            lisp.Run("(load \"uni\")");

            Expect(lisp, "(strlen g1)", "9");
            Expect(lisp, "(prin1 g1)", "\"a\u1ec5b\"\"a\u1ec5b\"");
        }
    }
}
