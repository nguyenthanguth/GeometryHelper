using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <see cref="CadLoadSecurity"/> by itself (decisions D901-D908 of the L6 spec): which folders are trusted and how
    /// TRUSTEDPATHS is read, the order in which SECURELOAD 0, 1 and 2, the trusted folders and the answers of the question
    /// decide a file, and the events. The rows of the TRUSTEDPATHS table are AutoCAD 2021's (research M1 and M4, console);
    /// the answers are G1-G3 (full AutoCAD). The files are made in a folder of the test's own under the temporary folder:
    /// <c>trusted\t.lsp</c>, <c>trusted\sub\s.lsp</c>, <c>trusted\sub\deep\d.lsp</c>, <c>trusted2\n.lsp</c> (a sibling that
    /// only shares the name's start), <c>other\o.lsp</c> and <c>p.lsp</c> in the root.
    /// </summary>
    public sealed class CadLoadSecurityTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gh-loadsec-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> _variables = new List<string>();

        // A hash as the settings file holds one: 64 hexadecimal digits, for the entries that are not made from a real file.
        private static readonly string HashA = new string('a', 64);
        private static readonly string HashB = new string('b', 64);

        public CadLoadSecurityTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            foreach (string name in _variables)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        // Makes a file under the root, folders included; its full path.
        private string Make(string relative, string text = "(princ)")
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path;
        }

        // The six files of the table; a folder part of an entry written {T} is the folder trusted, {R} the root.
        private string[] Table() =>
            new[]
            {
                Make(@"trusted\t.lsp"),
                Make(@"trusted\sub\s.lsp"),
                Make(@"trusted\sub\deep\d.lsp"),
                Make(@"trusted2\n.lsp"),
                Make(@"other\o.lsp"),
                Make("p.lsp"),
            };

        private string Entry(string template) =>
            template
                .Replace("{TU}", Path.Combine(_root, "trusted").ToUpperInvariant())
                .Replace("{T}", Path.Combine(_root, "trusted"))
                .Replace("{R}", _root);

        private static string Sha(string path)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter
                    .ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
        }

        // An Ask that must not be called.
        private static CadLoadAnswer NeverAsked(string path) =>
            throw new InvalidOperationException("The question was asked about " + path + ".");

        #region Defaults and limits

        [Fact]
        public void ANewInstance_StartsAsAutoCadDoes()
        {
            // AutoCAD 2021 (research M1): SECURELOAD 1, TRUSTEDPATHS the install folder only (ours: empty, the app folder is
            // always trusted), LEGACYCODESEARCH 0
            var security = new CadLoadSecurity();

            Assert.Equal(1, security.SecureLoad);
            Assert.Equal(string.Empty, security.TrustedPaths);
            Assert.False(security.LegacyCodeSearch);
            Assert.Empty(security.AlwaysLoaded);
            Assert.Null(security.Ask);
        }

        [Theory]
        [InlineData(3)] // AutoCAD 2021 (research M2): (setvar "SECURELOAD" 3) is rejected, the range is 0..2
        [InlineData(-1)]
        [InlineData(100)]
        public void ASecureLoadOutsideZeroToTwo_Throws_AndLeavesTheValue(int value)
        {
            var security = new CadLoadSecurity { SecureLoad = 2 };

            Assert.Throws<ArgumentOutOfRangeException>(() => security.SecureLoad = value);
            Assert.Equal(2, security.SecureLoad);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ASecureLoadOfZeroOneOrTwo_IsKept(int value)
        {
            var security = new CadLoadSecurity { SecureLoad = value };

            Assert.Equal(value, security.SecureLoad);
        }

        [Fact]
        public void TheProgramFolder_IsTheBaseDirectoryInFull_WithNoTrailingSeparator()
        {
            string expected = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');

            Assert.Equal(expected, CadLoadSecurity.ProgramFolder);
        }

        [Fact]
        public void APathIsRequired()
        {
            var security = new CadLoadSecurity();

            Assert.Throws<ArgumentNullException>(() => security.IsTrusted(null));
            Assert.Throws<ArgumentNullException>(() => security.MayLoad(null));
        }

        #endregion

        #region TRUSTEDPATHS

        // Columns: the file of trusted, of trusted\sub, of trusted\sub\deep, of the sibling trusted2, of other, and of the root.
        [Theory]
        [InlineData(@"{T}", true, false, false, false, false, false)] // AutoCAD 2021 (M1): the folder, not its subfolders
        [InlineData(@"{T}\...", true, true, true, false, false, false)] // M4: the folder and every subfolder at any depth
        [InlineData(@"{T}\sub\...", false, true, true, false, false, false)] // M4: ghl6sub and below, its parent's files refused
        [InlineData(@"{T}\", true, false, false, false, false, false)] // M4: a trailing backslash, the folder
        [InlineData(@"{T}\sub\..", true, false, false, false, false, false)] // M4: normalised to the parent, the parent trusted
        [InlineData(@"{TU}", true, false, false, false, false, false)] // M4: case ignored
        [InlineData(@"{T};{R}\other", true, false, false, false, true, false)] // M4: A;B trusts both
        [InlineData(@"  {T}  ", true, false, false, false, false, false)] // M4: spaces trimmed
        [InlineData(@"{T}\...   ", true, true, true, false, false, false)] // trimmed first, then the ending read (D904)
        [InlineData("\"{T}\"", false, false, false, false, false, false)] // M4: quotes are not taken off, nothing trusted
        [InlineData(@"{T};;", true, false, false, false, false, false)] // M4: empty entries skipped
        [InlineData(@";; ;", false, false, false, false, false, false)] // only empty entries
        [InlineData(@"{R}\trus*", false, false, false, false, false, false)] // M4: no wildcards (a literal name no folder has)
        [InlineData(@"{R}", false, false, false, false, false, true)] // M4: the parent trusts its own files, not the child's
        [InlineData(@"", false, false, false, false, false, false)] // M4: "" trusts nothing (the install folder still is)
        [InlineData(@"{T}/...", true, false, false, false, false, false)] // M2: forward slashes, the folder only
        [InlineData(@"{T}/sub\...", false, true, true, false, false, false)] // D904: the folder part goes through GetFullPath
        [InlineData(@"{R}\other\..\trusted", true, false, false, false, false, false)] // D904: x\.. is the parent
        [InlineData(@"{R}\a|b;{T}", true, false, false, false, false, false)] // GetFullPath refuses the first, the second stays
        [InlineData(@"{R}\...", true, true, true, true, true, true)] // everything under the root, siblings too
        public void ATrustedPathsEntry_TrustsTheFilesAutoCadTrusts(
            string entry,
            bool top,
            bool sub,
            bool deep,
            bool sibling,
            bool other,
            bool parent
        )
        {
            string[] files = Table();
            var security = new CadLoadSecurity { TrustedPaths = Entry(entry) };

            Assert.Equal(new[] { top, sub, deep, sibling, other, parent }, files.Select(security.IsTrusted).ToArray());
        }

        [Fact]
        public void AFolderThatOnlySharesTheStartOfTheName_IsNotTrusted_WithOrWithoutSubfolders()
        {
            // trusted2 shares "trusted" with the entry; a prefix test on the text would trust it
            string sibling = Make(@"trusted2\n.lsp");
            Make(@"trusted\t.lsp");

            Assert.False(new CadLoadSecurity { TrustedPaths = Entry(@"{T}\...") }.IsTrusted(sibling));
            Assert.False(new CadLoadSecurity { TrustedPaths = Entry(@"{T}") }.IsTrusted(sibling));
        }

        [Fact]
        public void APathLeavingATrustedFolderByDotDot_IsJudgedByItsFullPath()
        {
            Table();
            var security = new CadLoadSecurity { TrustedPaths = Entry(@"{T}\...") };

            // trusted\..\other\o.lsp is other\o.lsp; other\..\trusted\sub\s.lsp is trusted\sub\s.lsp
            Assert.False(security.IsTrusted(Path.Combine(_root, @"trusted\..\other\o.lsp")));
            Assert.True(security.IsTrusted(Path.Combine(_root, @"other\..\trusted\sub\s.lsp")));
        }

        [Fact]
        public void APathInAnotherCase_OrWrittenWithForwardSlashes_IsJudgedByItsFullPath()
        {
            // (load "C:/.../t.lsp") hands the gate a path with forward slashes, and Windows paths ignore case
            string[] files = Table();
            var security = new CadLoadSecurity { TrustedPaths = Entry(@"{T}\...") };

            Assert.True(security.IsTrusted(files[1].ToUpperInvariant()));
            Assert.True(security.IsTrusted(files[1].Replace('\\', '/')));
            Assert.False(security.IsTrusted(files[4].Replace('\\', '/')));
        }

        [Fact]
        public void ADriveRootWithTheThreeDots_TrustsEveryFileOnTheDrive()
        {
            // ours, from the rule of D904 and not measured: "C:\..." is the folder C:\ and every subfolder, and the folder
            // part C: alone would be GetFullPath's drive-relative form, the current folder of the drive
            string[] files = Table();
            var security = new CadLoadSecurity { TrustedPaths = Path.GetPathRoot(_root) + "..." };

            Assert.All(files, file => Assert.True(security.IsTrusted(file)));
        }

        [Fact]
        public void ARelativeEntry_IsSkipped()
        {
            // D904: an entry that is not rooted trusts nothing, not even the folder it would name from the current folder
            string parent = Path.GetFullPath("..");
            string file = Path.Combine(parent, "ghl6-relative-entry.lsp");
            Assert.False(file.StartsWith(CadLoadSecurity.ProgramFolder + "\\", StringComparison.OrdinalIgnoreCase));

            var security = new CadLoadSecurity { TrustedPaths = ".." };

            Assert.False(security.IsTrusted(file));
        }

        [Fact]
        public void TheProgramFolderAndItsSubfolders_AreAlwaysTrusted_ButNotAFolderThatSharesTheStartOfItsName()
        {
            // AutoCAD 2021 (M1): the install folder's acadinfo.lsp loads with TRUSTEDPATHS empty
            var security = new CadLoadSecurity { TrustedPaths = string.Empty };
            string program = CadLoadSecurity.ProgramFolder;

            Assert.True(security.IsTrusted(Path.Combine(program, "x.lsp")));
            Assert.True(security.IsTrusted(Path.Combine(program, @"python\deep\x.py")));
            Assert.False(security.IsTrusted(program + @"2\x.lsp"));
            Assert.False(security.IsTrusted(Path.Combine(Path.GetDirectoryName(program), "x.lsp")));
        }

        [Fact]
        public void TheProgramFolder_IsStillTrusted_WhenTrustedPathsNamesOthers()
        {
            var security = new CadLoadSecurity { TrustedPaths = Entry(@"{T}") };

            Assert.True(security.IsTrusted(Path.Combine(CadLoadSecurity.ProgramFolder, "x.lsp")));
        }

        [Fact]
        public void AnEnvironmentVariableInTrustedPaths_IsExpandedWhenItIsSet()
        {
            // AutoCAD 2021 (M4): (setvar "TRUSTEDPATHS" "%USERPROFILE%\\...") then (getvar "TRUSTEDPATHS") gives the expanded text
            string name = "GHL6_ROOT_" + Guid.NewGuid().ToString("N");
            _variables.Add(name);
            Environment.SetEnvironmentVariable(name, _root);
            string[] files = Table();

            var security = new CadLoadSecurity { TrustedPaths = "%" + name + @"%\trusted\..." };

            Assert.Equal(_root + @"\trusted\...", security.TrustedPaths);
            Assert.True(security.IsTrusted(files[1]));

            // expanded when it was set: the variable changing later changes nothing
            Environment.SetEnvironmentVariable(name, Path.Combine(_root, "other"));
            Assert.Equal(_root + @"\trusted\...", security.TrustedPaths);
            Assert.True(security.IsTrusted(files[1]));
        }

        [Fact]
        public void AnEnvironmentVariableThatIsNotSet_IsKeptAsTyped()
        {
            var security = new CadLoadSecurity { TrustedPaths = @"%GHL6_NO_SUCH_VARIABLE%\x" };

            Assert.Equal(@"%GHL6_NO_SUCH_VARIABLE%\x", security.TrustedPaths);
        }

        [Fact]
        public void TrustedPathsNull_IsEmpty_AndAnyOtherTextIsKeptAsTyped()
        {
            // AutoCAD 2021 (research D905): any string is taken
            var security = new CadLoadSecurity { TrustedPaths = "a<b;;  \"c\"" };

            Assert.Equal("a<b;;  \"c\"", security.TrustedPaths);

            security.TrustedPaths = null;

            Assert.Equal(string.Empty, security.TrustedPaths);
        }

        #endregion

        #region The decision

        [Fact]
        public void WithSecureLoadZero_EveryFileLoads_AndNothingIsAsked()
        {
            // AutoCAD 2021 (M2): SECURELOAD 0 loads the untrusted file and the one in a subfolder
            string[] files = Table();
            var security = new CadLoadSecurity { SecureLoad = 0, Ask = NeverAsked };

            Assert.All(files, file => Assert.True(security.MayLoad(file)));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void AFileInATrustedFolder_Loads_WhateverTheSecureLoad_AndNothingIsAsked(int level)
        {
            string[] files = Table();
            var security = new CadLoadSecurity
            {
                SecureLoad = level,
                TrustedPaths = Entry(@"{T}\..."),
                Ask = NeverAsked,
            };

            Assert.True(security.MayLoad(files[0]));
            Assert.True(security.MayLoad(files[2]));
        }

        [Fact]
        public void WithSecureLoadTwo_AnUntrustedFileIsRefused_AndNothingIsAsked()
        {
            // AutoCAD 2021 (M2, G1): SECURELOAD 2 gives no warning and the file is refused
            string other = Table()[4];
            var security = new CadLoadSecurity { SecureLoad = 2, Ask = NeverAsked };

            Assert.False(security.MayLoad(other));
        }

        [Fact]
        public void WithSecureLoadTwo_AnAlwaysLoadAnswerDoesNotCount()
        {
            // AutoCAD 2021 (G3): the Always Load file under SECURELOAD 2 was refused
            string other = Table()[4];
            var security = new CadLoadSecurity
            {
                SecureLoad = 2,
                AlwaysLoaded = new[] { Sha(other) + "|" + other },
                Ask = NeverAsked,
            };

            Assert.False(security.MayLoad(other));
        }

        [Fact]
        public void WithSecureLoadOneAndNoQuestion_AnUntrustedFileIsRefused()
        {
            // AutoCAD 2021 (M1): the console, which cannot ask, refuses
            string other = Table()[4];

            Assert.False(new CadLoadSecurity().MayLoad(other));
        }

        [Fact]
        public void LoadOnce_LoadsTheFile_AndTheSameFileIsNotAskedAgain_ButAnotherIs()
        {
            // AutoCAD 2021 (G1): Load Once loads, and the same file loads again without asking in that session
            string[] files = Table();
            var asked = new List<string>();
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked.Add(path);
                    return CadLoadAnswer.LoadOnce;
                },
            };

            Assert.True(security.MayLoad(files[4]));
            Assert.True(security.MayLoad(files[4]));
            Assert.True(security.MayLoad(files[5]));

            Assert.Equal(new[] { files[4], files[5] }, asked);
        }

        [Fact]
        public void LoadOnce_HoldsAfterTheFileIsEdited_InTheSameRun()
        {
            // ours (D906): G1 measured an unchanged file only; a Load Once is for the path for the life of the app
            string other = Table()[4];
            int asked = 0;
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked++;
                    return CadLoadAnswer.LoadOnce;
                },
            };
            security.MayLoad(other);

            File.AppendAllText(other, "\n; edited");

            Assert.True(security.MayLoad(other));
            Assert.Equal(1, asked);
        }

        [Fact]
        public void LoadOnce_IsNotKeptByAnotherInstance()
        {
            // AutoCAD 2021 (G3): the Load Once file in a new session was asked again
            string other = Table()[4];
            var first = new CadLoadSecurity { Ask = path => CadLoadAnswer.LoadOnce };
            var second = new CadLoadSecurity();

            Assert.True(first.MayLoad(other));

            Assert.False(second.MayLoad(other));
            Assert.Empty(first.AlwaysLoaded);
        }

        [Fact]
        public void DoNotLoad_RefusesTheFile_AndTheNextLoadAsksAgain()
        {
            // AutoCAD 2021 (G1): Do Not Load gives File load canceled; nothing is remembered about it
            string other = Table()[4];
            int asked = 0;
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked++;
                    return CadLoadAnswer.DoNotLoad;
                },
            };

            Assert.False(security.MayLoad(other));
            Assert.False(security.MayLoad(other));

            Assert.Equal(2, asked);
            Assert.Empty(security.AlwaysLoaded);
        }

        [Fact]
        public void AskIsGivenTheFullPath_OfAPathWrittenWithDotDot()
        {
            string[] files = Table();
            string asked = null;
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked = path;
                    return CadLoadAnswer.DoNotLoad;
                },
            };

            security.MayLoad(Path.Combine(_root, @"trusted\..\other\o.lsp"));

            Assert.Equal(files[4], asked);
        }

        [Fact]
        public void AlwaysLoad_LoadsTheFile_AndKeepsItsPathAndHash()
        {
            // AutoCAD 2021 (G1): Always Load loads, and the same file loads again without asking
            string other = Table()[4];
            int asked = 0;
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked++;
                    return CadLoadAnswer.AlwaysLoad;
                },
            };

            Assert.True(security.MayLoad(other));
            Assert.True(security.MayLoad(other));

            Assert.Equal(1, asked);
            Assert.Equal(new[] { Sha(other) + "|" + other }, security.AlwaysLoaded);
        }

        [Fact]
        public void AlwaysLoad_DoesNotTrustTheFolder_SoAnotherFileOfItIsAsked()
        {
            // AutoCAD 2021 (G1): TRUSTEDPATHS unchanged, another file of the same folder is asked again
            string[] files = Table();
            Make(@"other\o2.lsp");
            var asked = new List<string>();
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked.Add(path);
                    return CadLoadAnswer.AlwaysLoad;
                },
            };

            security.MayLoad(files[4]);
            security.MayLoad(Path.Combine(_root, @"other\o2.lsp"));

            Assert.Equal(2, asked.Count);
            Assert.Equal(string.Empty, security.TrustedPaths);
        }

        [Fact]
        public void AnAlwaysLoadAnswer_IsKeptByANewInstanceGivenTheSameList()
        {
            // AutoCAD 2021 (G2): in a new session the Always Load file loads with no warning
            string other = Table()[4];
            var first = new CadLoadSecurity { Ask = path => CadLoadAnswer.AlwaysLoad };
            first.MayLoad(other);
            var second = new CadLoadSecurity { AlwaysLoaded = first.AlwaysLoaded, Ask = NeverAsked };

            Assert.True(second.MayLoad(other));
        }

        [Fact]
        public void AnAlwaysLoadFileWhoseBytesChanged_IsAskedAgain()
        {
            // AutoCAD 2021 (G3): after one comment line was appended the file was asked again; 15 bytes added here
            string other = Table()[4];
            var security = new CadLoadSecurity { Ask = path => CadLoadAnswer.AlwaysLoad };
            security.MayLoad(other);
            File.AppendAllText(other, "\n; one comment\n");
            var answers = new Queue<CadLoadAnswer>(new[] { CadLoadAnswer.DoNotLoad, CadLoadAnswer.AlwaysLoad });
            var asked = new List<string>();
            security.Ask = path =>
            {
                asked.Add(path);
                return answers.Dequeue();
            };

            Assert.False(security.MayLoad(other));
            Assert.Equal(new[] { other }, asked);

            // Always Load again takes the new content
            Assert.True(security.MayLoad(other));
            Assert.Contains(Sha(other) + "|" + other, security.AlwaysLoaded);
            Assert.True(security.MayLoad(other));
            Assert.Equal(2, asked.Count);
        }

        [Fact]
        public void AnAlwaysLoadEntry_IsMatchedByPathWithoutRegardToCase()
        {
            // D904 and the spec's rule for paths: Windows paths are compared case-insensitively
            string other = Table()[4];
            var security = new CadLoadSecurity
            {
                AlwaysLoaded = new[] { Sha(other) + "|" + other.ToUpperInvariant() },
                Ask = NeverAsked,
            };

            Assert.True(security.MayLoad(other.ToLowerInvariant()));
        }

        [Fact]
        public void AnAlwaysLoadEntryOfAnotherFileWithTheSameBytes_DoesNotLoadThisOne()
        {
            // the answer is for a path and its content; a copy elsewhere is a different file
            string other = Table()[4];
            string copy = Make(@"other\copy.lsp", File.ReadAllText(other));
            var security = new CadLoadSecurity { AlwaysLoaded = new[] { Sha(other) + "|" + other } };

            Assert.True(security.MayLoad(other));
            Assert.False(security.MayLoad(copy));
        }

        [Fact]
        public void MalformedAlwaysLoadedEntries_TrustNothing_AndDoNotThrow()
        {
            // ours: the list comes from the settings file, which a user can edit
            string other = Table()[4];
            var security = new CadLoadSecurity
            {
                AlwaysLoaded = new[] { "nonsense", string.Empty, "|", "abc|", "|" + other },
            };

            Assert.False(security.MayLoad(other));
        }

        [Fact]
        public void AnAlwaysLoadedListOfNull_IsEmpty_AndTheListIsKeptAsGiven()
        {
            string[] entries = { HashA + @"|C:\a.lsp", HashB + @"|C:\b.lsp" };
            var security = new CadLoadSecurity { AlwaysLoaded = entries };

            Assert.Equal(entries, security.AlwaysLoaded);

            security.AlwaysLoaded = null;

            Assert.Empty(security.AlwaysLoaded);
        }

        [Fact]
        public void ForgetAlwaysLoaded_EmptiesTheList_AndTheFileIsAskedAgain()
        {
            string other = Table()[4];
            int asked = 0;
            var security = new CadLoadSecurity
            {
                Ask = path =>
                {
                    asked++;
                    return CadLoadAnswer.AlwaysLoad;
                },
            };
            security.MayLoad(other);

            security.ForgetAlwaysLoaded();
            security.MayLoad(other);

            Assert.Equal(2, asked);
        }

        [Fact]
        public void ManyThreadsLoadingTheSameFile_AllGetYes_AndTheListHoldsItOnce()
        {
            // D906: the gate is thread-agnostic, the window marshals the question; sixteen loads at once from the pool
            string other = Table()[4];
            var security = new CadLoadSecurity { Ask = path => CadLoadAnswer.AlwaysLoad };
            var answers = new bool[16];

            System.Threading.Tasks.Parallel.For(0, answers.Length, i => answers[i] = security.MayLoad(other));

            Assert.All(answers, answer => Assert.True(answer));
            Assert.Single(security.AlwaysLoaded.Distinct());
        }

        [Fact]
        public void AnAskThatThrows_IsARefusal_AndTheFailureIsLoggedAsAWarning()
        {
            string other = Table()[4];
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Writer = (level, text, exception) =>
                {
                    if (text.Contains(other))
                    {
                        lock (lines)
                        {
                            lines.Add(level + " " + text);
                        }
                    }
                };

                var security = new CadLoadSecurity { Ask = path => throw new InvalidOperationException("no window") };

                Assert.False(security.MayLoad(other));
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            Assert.Contains("Warn Load security: the question about " + other + " failed: no window", lines);
        }

        [Fact]
        public void ARefusal_IsLoggedOnce_AsADebugLine_WithTheSecureLoad()
        {
            string other = Table()[4];
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Writer = (level, text, exception) =>
                {
                    if (text.Contains(other))
                    {
                        lock (lines)
                        {
                            lines.Add(level + " " + text);
                        }
                    }
                };

                Assert.False(new CadLoadSecurity { SecureLoad = 2 }.MayLoad(other));
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            Assert.Equal(new[] { "Debug Load security: refused " + other + " (SECURELOAD 2)" }, lines);
        }

        #endregion

        #region Changed

        [Fact]
        public void Changed_IsRaisedByEachPropertyThatChanges()
        {
            var security = new CadLoadSecurity();
            int raised = 0;
            security.Changed += (sender, e) => raised++;

            security.SecureLoad = 2;
            Assert.Equal(1, raised);

            security.TrustedPaths = Entry(@"{T}");
            Assert.Equal(2, raised);

            security.LegacyCodeSearch = true;
            Assert.Equal(3, raised);

            security.AlwaysLoaded = new[] { HashA + @"|C:\a.lsp" };
            Assert.Equal(4, raised);

            security.ForgetAlwaysLoaded();
            Assert.Equal(5, raised);
        }

        [Fact]
        public void Changed_IsRaisedWithTheInstanceAsItsSender()
        {
            var security = new CadLoadSecurity();
            object seen = null;
            security.Changed += (sender, e) => seen = sender;

            security.SecureLoad = 0;

            Assert.Same(security, seen);
        }

        [Fact]
        public void Changed_IsRaisedByAnAlwaysLoadAnswer_ButNotByLoadOnceOrDoNotLoad()
        {
            string[] files = Table();
            var answers = new Queue<CadLoadAnswer>(
                new[] { CadLoadAnswer.LoadOnce, CadLoadAnswer.DoNotLoad, CadLoadAnswer.AlwaysLoad }
            );
            var security = new CadLoadSecurity { Ask = path => answers.Dequeue() };
            int raised = 0;
            security.Changed += (sender, e) => raised++;

            security.MayLoad(files[4]);
            security.MayLoad(files[5]);
            Assert.Equal(0, raised);

            security.MayLoad(files[0]);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void ASecureLoadThatIsRefused_RaisesNoChanged()
        {
            var security = new CadLoadSecurity();
            int raised = 0;
            security.Changed += (sender, e) => raised++;

            Assert.Throws<ArgumentOutOfRangeException>(() => security.SecureLoad = 3);

            Assert.Equal(0, raised);
        }

        #endregion

        #region Code files

        [Theory]
        [InlineData("a.lsp")] // AutoCAD 2021 (M5): lsp dcl scr fas vlx mnl dll arx dvb cuix leave out the start-in folder
        [InlineData("a.dcl")]
        [InlineData("a.scr")]
        [InlineData("a.fas")]
        [InlineData("a.vlx")]
        [InlineData("a.mnl")]
        [InlineData("a.dll")]
        [InlineData("a.arx")]
        [InlineData("a.dvb")]
        [InlineData("a.cuix")]
        [InlineData("a.py")] // ours (D908)
        [InlineData("A.LSP")]
        [InlineData("a.Cuix")]
        [InlineData(@"C:\folder.lsp\a.py")]
        [InlineData("a.b.lsp")]
        public void IsCodeFile_IsTrue_ForTheElevenExtensions_InAnyCase(string path)
        {
            Assert.True(CadLoadSecurity.IsCodeFile(path));
        }

        [Theory]
        [InlineData("a.txt")] // AutoCAD 2021 (M5): txt js lin pat are found beside the drawing
        [InlineData("a.js")]
        [InlineData("a.lin")]
        [InlineData("a.pat")]
        [InlineData("a.dwg")]
        [InlineData("a")]
        [InlineData("lsp")]
        [InlineData("a.lsp.txt")]
        [InlineData(@"C:\folder.lsp\a")]
        [InlineData("")]
        public void IsCodeFile_IsFalse_ForAnyOtherName(string path)
        {
            Assert.False(CadLoadSecurity.IsCodeFile(path));
        }

        #endregion
    }
}
