using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What <c>load</c> and everything built on it does with SECURELOAD, TRUSTEDPATHS and LEGACYCODESEARCH, typed at the
    /// command line of a session (decisions D901-D910 of the L6 spec), against AutoCAD 2021's accoreconsole (research M1-M5)
    /// and the full AutoCAD (G1-G3). A session made by the library starts at SECURELOAD 0, as no window is there to ask; each
    /// test sets the level it needs with <c>setvar</c>, as a user does. The files are made in a folder of the test's own under
    /// the temporary folder: <c>untrusted\u1.lsp</c>, <c>trusted\t1.lsp</c> and <c>trusted\sub\s1.lsp</c>, each setting a
    /// symbol of its own to 1 and giving 42.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispLoadSecurityTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-loadsec-" + Guid.NewGuid().ToString("N")
        );

        public LispLoadSecurityTests()
        {
            LispApplication.Reset();
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();
            LispApplication.Reset();

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        private string Make(string relative, string text)
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path;
        }

        // The three files of the fixture; the symbol each sets is ghl6u1, ghl6t1 and ghl6s1.
        private string Untrusted() => Make(@"untrusted\u1.lsp", "(setq ghl6u1 1)\n42\n");

        private string Trusted() => Make(@"trusted\t1.lsp", "(setq ghl6t1 1)\n42\n");

        private string Sub() => Make(@"trusted\sub\s1.lsp", "(setq ghl6s1 1)\n42\n");

        private string Folder(string name) => Path.Combine(_root, name);

        // A path as a routine writes it in a string, forward slashes: the error names it as it was given.
        private static string Slashed(string path) => path.Replace('\\', '/');

        // A path as a routine writes it in a string, backslashes doubled.
        private static string Quoted(string text) => "\"" + text.Replace("\\", "\\\\") + "\"";

        // A path as prin1 writes it.
        private static string Printed(string path) => Quoted(path);

        private static string LoadForm(string path) => "(load \"" + Slashed(path) + "\")";

        private static string Canceled(string path) => "; error: File load canceled: " + Slashed(path);

        // What a form gives, as the line after it.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        // A session of the library with the level set as a user sets it, and the trusted folders if there are any.
        private static CadCommandSession Level(int secureLoad, string trustedPaths = null)
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"SECURELOAD\" " + secureLoad + ")");

            if (trustedPaths != null)
            {
                Value(session, "(setvar \"TRUSTEDPATHS\" " + Quoted(trustedPaths) + ")");
            }

            return session;
        }

        #region The variables

        [Fact]
        public void ASessionOfTheLibrary_StartsAtSecureLoadZero_WithNoTrustedPaths_AndNoLegacySearch()
        {
            // ours (D903): no window to ask, so every file loads as before; AutoCAD's own initial value is 1 (research M1)
            CadCommandSession session = Session(Blank());

            Assert.Equal("0", Value(session, "(getvar \"SECURELOAD\")"));
            Assert.Equal("\"\"", Value(session, "(getvar \"TRUSTEDPATHS\")"));
            Assert.Equal("0", Value(session, "(getvar \"LEGACYCODESEARCH\")"));
        }

        [Fact]
        public void ASessionOfTheLibrary_LoadsAFileFromAnywhere_AsBefore()
        {
            // the guard of D903: every load test of the suite loads from a temporary folder with no variable set
            CadCommandSession session = Session(Blank());

            Assert.Equal("42", Value(session, LoadForm(Untrusted())));
            Assert.Equal("1", Value(session, "!ghl6u1"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Setvar_OfSecureLoadZeroOneOrTwo_IsTakenAndGivenBack(int level)
        {
            // AutoCAD 2021 (M2): SECURELOAD takes 0..2
            CadCommandSession session = Session(Blank());

            Assert.Equal(level.ToString(), Value(session, "(setvar \"SECURELOAD\" " + level + ")"));
            Assert.Equal(level.ToString(), Value(session, "(getvar \"SECURELOAD\")"));
        }

        [Theory]
        [InlineData("(setvar \"SECURELOAD\" 3)", "\"SECURELOAD\" 3")] // AutoCAD 2021 (M2): rejected, the range is 0..2
        [InlineData("(setvar \"SECURELOAD\" -1)", "\"SECURELOAD\" -1")]
        [InlineData("(setvar \"SECURELOAD\" \"x\")", "\"SECURELOAD\" \"x\"")]
        [InlineData("(setvar \"LEGACYCODESEARCH\" 2)", "\"LEGACYCODESEARCH\" 2")] // 0..1 as every two-state variable
        [InlineData("(setvar \"LEGACYCODESEARCH\" -1)", "\"LEGACYCODESEARCH\" -1")]
        public void Setvar_OfAValueOutOfRange_IsRejectedNamingTheVariableAndTheValue(string form, string shown)
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"SECURELOAD\" 2)");

            Assert.Equal("; error: AutoCAD variable setting rejected: " + shown, Value(session, form));
            Assert.Equal("2", Value(session, "(getvar \"SECURELOAD\")"));
            Assert.Equal("0", Value(session, "(getvar \"LEGACYCODESEARCH\")"));
        }

        [Fact]
        public void Setvar_OfLegacyCodeSearch_TakesZeroAndOne()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("1", Value(session, "(setvar \"LEGACYCODESEARCH\" 1)"));
            Assert.Equal("1", Value(session, "(getvar \"LEGACYCODESEARCH\")"));
            Assert.Equal("0", Value(session, "(setvar \"LEGACYCODESEARCH\" 0)"));
        }

        [Fact]
        public void TrustedPaths_IsKeptAsTyped_AndGivenBack()
        {
            CadCommandSession session = Session(Blank());
            string typed = Folder("trusted") + @"\...;" + Folder("other");

            Assert.Equal(Printed(typed), Value(session, "(setvar \"TRUSTEDPATHS\" " + Quoted(typed) + ")"));
            Assert.Equal(Printed(typed), Value(session, "(getvar \"TRUSTEDPATHS\")"));
        }

        [Fact]
        public void TrustedPaths_GivesTheExpandedText_OfAnEnvironmentVariableSetBefore()
        {
            // AutoCAD 2021 (M4): (setvar "TRUSTEDPATHS" "%USERPROFILE%\\...") then getvar gives C:\Users\<name>\...
            string name = "GHL6_SESSION_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(name, _root);

            try
            {
                CadCommandSession session = Session(Blank());

                Value(session, "(setvar \"TRUSTEDPATHS\" " + Quoted("%" + name + @"%\...") + ")");

                Assert.Equal(Printed(_root + @"\..."), Value(session, "(getvar \"TRUSTEDPATHS\")"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [Fact]
        public void TheSetvarCommand_TakesSecureLoad_AndAsksItsCurrentValue()
        {
            // the command line's SETVAR, as the other integer variables: the prompt shows the value, a value out of range is refused
            CadCommandSession session = Session(Blank());

            Run(session, "SETVAR", "SECURELOAD");
            Assert.Equal("Enter new value for SECURELOAD <0>:", session.PromptText);
            Run(session, "2");
            Run(session, "SETVAR", "SECURELOAD", "7");
            session.Escape();

            Assert.Equal("2", Value(session, "(getvar \"SECURELOAD\")"));
        }

        [Fact]
        public void TwoSessionsOfTheLibrary_EachKeepTheirOwnLevel()
        {
            CadCommandSession first = Session(Blank());
            CadCommandSession second = Session(Blank());

            Value(first, "(setvar \"SECURELOAD\" 2)");

            Assert.Equal("2", Value(first, "(getvar \"SECURELOAD\")"));
            Assert.Equal("0", Value(second, "(getvar \"SECURELOAD\")"));
        }

        #endregion

        #region load

        [Fact]
        public void WithSecureLoadOne_AFileOutsideTheTrustedFolders_IsCanceled_AsTheNameWasGiven()
        {
            // AutoCAD 2021 (M1): (load "D:/.../u1.lsp") ; error: File load canceled: D:/.../u1.lsp, nothing defined
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void ARefusedFile_IsNotReadAtAll_SoAnErrorInsideItIsNeverMet()
        {
            // the gate comes before the reading: a file that would fail loudly is only canceled
            string path = Make(@"untrusted\bad.lsp", "(car 1)\n(defun broken (\n");
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
        }

        [Fact]
        public void TheFallbackOfLoad_IsNotUsed_ForARefusedFile()
        {
            // AutoCAD 2021 (M1, G1): (load f "FALLBACK") is the same error: the second argument is not returned
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(path), Value(session, "(load \"" + Slashed(path) + "\" \"FALLBACK\")"));
        }

        [Fact]
        public void TheRefusal_IsAnErrorThatCanBeCaught_WithTheMessageOfAutoCad()
        {
            // AutoCAD 2021 (M1): vl-catch-all-error-message gives "File load canceled: D:/.../u1.lsp"
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.Equal(
                "\"File load canceled: " + Slashed(path) + "\"",
                Value(
                    session,
                    "(vl-catch-all-error-message (vl-catch-all-apply 'load (list \"" + Slashed(path) + "\")))"
                )
            );
        }

        [Fact]
        public void TheRefusal_CallsTheErrorFunction_WithTheMessage()
        {
            string path = Untrusted();
            CadCommandSession session = Level(1);
            Value(session, "(defun *error* (m) (setq ghl6msg m))");

            Value(session, LoadForm(path));

            Assert.Equal("\"File load canceled: " + Slashed(path) + "\"", Value(session, "!ghl6msg"));
        }

        [Fact]
        public void WithSecureLoadTwo_AFileOutsideTheTrustedFolders_IsCanceledTheSame()
        {
            // AutoCAD 2021 (M2): SECURELOAD 2 gives the same text
            string path = Untrusted();
            CadCommandSession session = Level(2);

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void WithSecureLoadZero_TheFileLoads_AndSoDoesOneInASubfolder()
        {
            // AutoCAD 2021 (M2): SECURELOAD 0, the untrusted file and the subfolder file load
            CadCommandSession session = Level(0);

            Assert.Equal("42", Value(session, LoadForm(Untrusted())));
            Assert.Equal("42", Value(session, LoadForm(Sub())));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void AFileInATrustedFolder_Loads(int level)
        {
            // AutoCAD 2021 (M1): a file of a TRUSTEDPATHS folder loads at SECURELOAD 1 and 2
            string path = Trusted();
            CadCommandSession session = Level(level, Folder("trusted"));

            Assert.Equal("42", Value(session, LoadForm(path)));
            Assert.Equal("1", Value(session, "!ghl6t1"));
        }

        [Fact]
        public void AFileInASubfolder_IsCanceledWithoutTheThreeDots_AndLoadsWithThem()
        {
            // AutoCAD 2021 (M1, M4): the folder alone does not trust its subfolders; "<folder>\..." trusts them at any depth
            string path = Sub();
            Trusted();
            CadCommandSession session = Level(1, Folder("trusted"));

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));

            Value(session, "(setvar \"TRUSTEDPATHS\" " + Quoted(Folder("trusted") + @"\...") + ")");

            Assert.Equal("42", Value(session, LoadForm(path)));
        }

        [Fact]
        public void TheLevelCanBeChangedBetweenLoads_InTheSameSession()
        {
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));

            Value(session, "(setvar \"SECURELOAD\" 0)");
            Assert.Equal("42", Value(session, LoadForm(path)));

            Value(session, "(setvar \"SECURELOAD\" 2)");
            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
        }

        [Fact]
        public void ANameThatLeavesATrustedFolderByDotDot_IsCanceled()
        {
            // the name is judged by the file it reaches: trusted\..\untrusted\u1.lsp is untrusted\u1.lsp
            Untrusted();
            Trusted();
            string name = Slashed(Folder("trusted")) + "/../untrusted/u1.lsp";
            CadCommandSession session = Level(1, Folder("trusted") + @"\...");

            Assert.Equal("; error: File load canceled: " + name, Value(session, "(load \"" + name + "\")"));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void ATrustedFileThatLoadsAnUntrustedOne_StopsThereWithTheCancel_AndKeepsWhatItDidBefore()
        {
            // the gate is on every load, nested ones too: the trusted file ran up to the refused load
            string untrusted = Untrusted();
            Make(
                @"trusted\outer.lsp",
                "(setq ghl6before 1)\n(load \"" + Slashed(untrusted) + "\")\n(setq ghl6after 1)\n"
            );
            CadCommandSession session = Level(1, Folder("trusted"));

            Assert.Equal(Canceled(untrusted), Value(session, LoadForm(Path.Combine(Folder("trusted"), "outer.lsp"))));
            Assert.Equal("1", Value(session, "!ghl6before"));
            Assert.Equal("nil", Value(session, "!ghl6after"));
            Assert.Equal("nil", Value(session, "!ghl6u1"));
        }

        [Fact]
        public void AMissingFile_IsALoadFailure_NotAQuestionOrACancel()
        {
            // only an existing file is checked: a file that is not there is LOAD failed at every level
            string missing = Folder(@"untrusted\nothere.lsp");
            CadCommandSession session = Level(1);

            Assert.Equal("; error: LOAD failed: " + Quoted(Slashed(missing)), Value(session, LoadForm(missing)));
            Assert.Equal("\"fb\"", Value(session, "(load \"" + Slashed(missing) + "\" \"fb\")"));
        }

        [Fact]
        public void AFileFoundOnTheSupportPath_IsCheckedToo_AndPassedOverWhenRefused()
        {
            // AutoCAD 2021 (research D907): a refused file found by a search is passed over: LOAD failed, and the fallback is used
            Untrusted();
            CadCommandSession session = Level(1);
            session.Document.SupportFolders = new[] { Folder("untrusted") };

            Assert.Equal("; error: LOAD failed: \"u1\"", Value(session, "(load \"u1\")"));
            Assert.Equal("\"fb\"", Value(session, "(load \"u1\" \"fb\")"));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void AFileFoundOnTheSupportPath_Loads_WhenItsFolderIsTrusted()
        {
            Untrusted();
            CadCommandSession session = Level(1, Folder("untrusted"));
            session.Document.SupportFolders = new[] { Folder("untrusted") };

            Assert.Equal("42", Value(session, "(load \"u1\")"));
        }

        [Fact]
        public void TheSearchGoesOn_PastARefusedFile_ToATrustedOneOfTheSameName()
        {
            // the first support folder is not trusted, the second is: the second one's file loads
            Make(@"untrusted\same.lsp", "(setq ghl6which 'untrusted)");
            Make(@"trusted\same.lsp", "(setq ghl6which 'trusted)");
            CadCommandSession session = Level(1, Folder("trusted"));
            session.Document.SupportFolders = new[] { Folder("untrusted"), Folder("trusted") };

            Value(session, "(load \"same\")");

            Assert.Equal("TRUSTED", Value(session, "!ghl6which"));
        }

        [Fact]
        public void AnUntrustedCompiledFile_IsCanceled_AsTheOtherFiles_BeforeTheCompiledRefusal()
        {
            // AutoCAD 2021 (M1): the .fas of an untrusted folder is refused the same way
            string fas = Make(@"untrusted\c1.fas", "not really compiled");
            string vlx = Make(@"untrusted\c2.vlx", "not really compiled");
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(fas), Value(session, LoadForm(fas)));
            Assert.Equal(Canceled(vlx), Value(session, LoadForm(vlx)));
            Assert.Equal(Canceled(fas), Value(session, "(load \"" + Slashed(fas) + "\" \"fb\")"));
        }

        [Fact]
        public void ATrustedCompiledFile_KeepsTheRefusalOfCompiledAutoLisp()
        {
            string fas = Make(@"trusted\c1.fas", "not really compiled");
            CadCommandSession session = Level(1, Folder("trusted"));

            Assert.Equal(
                "; error: LOAD failed: " + Quoted(Slashed(fas)) + " (compiled AutoLISP is not supported)",
                Value(session, LoadForm(fas))
            );
        }

        [Fact]
        public void ACompiledFileThatIsNotThere_IsTheOldRefusal_NotACancel()
        {
            // only an existing file is checked
            string fas = Folder(@"untrusted\nothere.fas");
            CadCommandSession session = Level(1);

            Assert.Equal(
                "; error: LOAD failed: " + Quoted(Slashed(fas)) + " (compiled AutoLISP is not supported)",
                Value(session, LoadForm(fas))
            );
        }

        [Fact]
        public void AppLoad_OfAnUntrustedFile_WritesTheCancel_ReturnsFalse_AndNeverSaysItLoaded()
        {
            // APPLOAD goes through Load: the refusal is the line, not "u1.lsp successfully loaded."
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.False(session.LoadLisp(path));

            Assert.Equal("; error: File load canceled: " + path, session.History.Last());
            Assert.DoesNotContain("u1.lsp successfully loaded.", session.History);
            Assert.Empty(session.LoadedLispFiles);
        }

        [Fact]
        public void AppLoad_OfATrustedFile_LoadsAndSaysSo()
        {
            string path = Trusted();
            CadCommandSession session = Level(1, Folder("trusted"));

            Assert.True(session.LoadLisp(path));

            Assert.Equal("t1.lsp successfully loaded.", session.History.Last());
        }

        [Fact]
        public void VlLoadAll_OfAnUntrustedFile_IsCanceled()
        {
            string path = Untrusted();
            CadCommandSession session = Level(1);

            Assert.Equal(Canceled(path), Value(session, "(vl-load-all \"" + Slashed(path) + "\")"));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void AFileVlLoadAllBringsIntoAnotherDrawing_IsChecked_ByThatDrawing()
        {
            // D901: the drawing that loaded it allows it (SECURELOAD 0); the other one, at 1, does not take it
            string path = Make(@"untrusted\v1.lsp", "(setq ghl6v1 1)\n42\n");
            CadCommandSession allowing = Level(0);
            CadCommandSession strict = Level(1);

            Assert.Equal("42", Value(allowing, "(vl-load-all \"" + Slashed(path) + "\")"));

            Assert.Equal("1", Value(allowing, "!ghl6v1"));
            Assert.Equal("nil", Value(strict, "(boundp 'ghl6v1)"));
        }

        #endregion

        #region What is not checked

        [Fact]
        public void LoadDialog_OfAnUntrustedDclFile_IsNotChecked_AtSecureLoadTwo()
        {
            // AutoCAD 2021 (G1): load_dialog of an untrusted .dcl gave 1 with no warning; DCL is not code
            string dcl = Make(@"untrusted\d1.dcl", "ghl6d : dialog { label = \"x\"; ok_only; }\n");
            CadCommandSession session = Level(2);

            Assert.NotEqual("-1", Value(session, "(load_dialog \"" + Slashed(dcl) + "\")"));
        }

        [Fact]
        public void FindfileAndOpen_OfAnUntrustedFile_AreNotChecked_AtSecureLoadTwo()
        {
            // AutoCAD 2021 (M1, G1): findfile and open are not gated
            string path = Untrusted();
            CadCommandSession session = Level(2);

            Assert.Equal(Printed(path), Value(session, "(findfile \"" + Slashed(path) + "\")"));
            Assert.Equal(
                "\"opened\"",
                Value(
                    session,
                    "(progn (setq ghl6h (open \""
                        + Slashed(path)
                        + "\" \"r\")) (close ghl6h) (if ghl6h \"opened\" \"not\"))"
                )
            );
        }

        #endregion

        #region Startup

        // A session whose drawing was just opened, with the level set before the startup files ran.
        private CadCommandSession Opened(LispStartup startup, int level, string trusted, params string[] support)
        {
            CadCommandSession session = Session(Blank());
            session.Document.SupportFolders = support;
            session.Lisp.Run("(setvar \"SECURELOAD\" " + level + ")");

            if (trusted != null)
            {
                session.Lisp.Run("(setvar \"TRUSTEDPATHS\" " + Quoted(trusted) + ")");
            }

            startup.OnDocumentOpened(session);
            return session;
        }

        [Fact]
        public void AnAcaddocLsp_InAnUntrustedSupportFolder_IsCanceled_NotLoaded()
        {
            string path = Make(@"untrusted\acaddoc.lsp", "(princ \"[acaddoc]\")");

            CadCommandSession session = Opened(new LispStartup(), 1, null, Folder("untrusted"));

            Assert.Equal(new[] { "; error: File load canceled: " + path }, session.History);
        }

        [Fact]
        public void AnAcaddocLsp_InATrustedSupportFolder_Loads()
        {
            Make(@"trusted\acaddoc.lsp", "(princ \"[acaddoc]\")");

            CadCommandSession session = Opened(new LispStartup(), 1, Folder("trusted"), Folder("trusted"));

            Assert.Equal(new[] { "[acaddoc]" }, session.History);
        }

        [Fact]
        public void AFileOfTheStartupSuite_OutsideTheTrustedFolders_IsCanceled_AndTheNextOneLoads()
        {
            string refused = Make(@"untrusted\suite1.lsp", "(princ \"[suite1]\")");
            string allowed = Make(@"trusted\suite2.lsp", "(princ \"[suite2]\")");
            var startup = new LispStartup();
            startup.StartupSuite.Add(refused);
            startup.StartupSuite.Add(allowed);

            CadCommandSession session = Opened(startup, 1, Folder("trusted"));

            Assert.Equal(new[] { "; error: File load canceled: " + refused, "[suite2]" }, session.History);
        }

        #endregion
    }
}
