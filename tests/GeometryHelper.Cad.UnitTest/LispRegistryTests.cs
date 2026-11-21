using System;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>vl-registry-read</c>, <c>-write</c>, <c>-delete</c> and <c>-descendents</c> against AutoCAD 2021's accoreconsole
    /// (probe r4, L3), on the presenter's own store: in memory for one interpreter, or in the file of
    /// <see cref="LispInterpreter.RegistryFile"/> shared by every interpreter given it. Nothing here touches the Windows
    /// registry; the files are made in a folder of its own under the temporary folder and removed after.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispRegistryTests : IDisposable
    {
        private const string Key = @"""HKEY_CURRENT_USER\\Software\\GhL3Probe""";
        private const string Sub = @"""HKEY_CURRENT_USER\\Software\\GhL3Probe\\Sub""";

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-registry-" + Guid.NewGuid().ToString("N")
        );

        public LispRegistryTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private string RegistryPath => Path.Combine(_folder, "lisp-registry.txt");

        private static void Expect(LispInterpreter lisp, string expression, string output) =>
            Assert.Equal(output, lisp.Run(expression).Output);

        /// <summary>Makes what r4 made: S and N in the key and a default value in Sub.</summary>
        private static void WriteProbeKeys(LispInterpreter lisp)
        {
            Expect(lisp, "(vl-registry-write " + Key + " \"S\" \"text\")", "\"text\"");
            Expect(lisp, "(vl-registry-write " + Key + " \"N\" 42)", "42");
            Expect(lisp, "(vl-registry-write " + Sub + " \"\" \"def\")", "\"def\"");
        }

        [Fact]
        public void TheRegistryFile_IsNullByDefault()
        {
            Assert.Null(new LispInterpreter().RegistryFile);
        }

        [Fact]
        public void WritingAndReading_InMemory_FollowR4()
        {
            // AutoCAD 2021 (r4): write S "text" -> "text", N 42 -> 42, Sub "" "def" -> "def"; read gives the same; a value that is
            // not there, and a key that is not there, give nil; Sub's default value is read with no name
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(lisp, "(vl-registry-read " + Key + " \"S\")", "\"text\"");
            Expect(lisp, "(vl-registry-read " + Key + " \"N\")", "42");
            Expect(lisp, "(vl-registry-read " + Sub + ")", "\"def\"");
            Expect(lisp, "(vl-registry-read " + Key + " \"nosuch\")", "nil");
            Expect(lisp, @"(vl-registry-read ""HKEY_CURRENT_USER\\Software\\GhL3Probe\\Nosuch"" ""S"")", "nil");
        }

        [Fact]
        public void ARealOrAList_IsRefused_AndNothingIsKept()
        {
            // AutoCAD 2021 (r4): (vl-registry-write key "R" 1.5) ; error: bad argument value: unsupported registry value: "R"
            // (vl-registry-read key "R") nil; the same for '(1 2) written as "L"
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(
                lisp,
                "(vl-registry-write " + Key + " \"R\" 1.5)",
                "; error: bad argument value: unsupported registry value: \"R\""
            );
            Expect(lisp, "(vl-registry-read " + Key + " \"R\")", "nil");
            Expect(
                lisp,
                "(vl-registry-write " + Key + " \"L\" '(1 2))",
                "; error: bad argument value: unsupported registry value: \"L\""
            );
            Expect(lisp, "(vl-registry-descendents " + Key + " T)", "(\"N\" \"S\")");
        }

        [Fact]
        public void TheRoot_MustBeWrittenInCapitalsInFull()
        {
            // AutoCAD 2021 (r4): a lower-case root and HKCU are ; error: bad registry key name: "..."
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(
                lisp,
                @"(vl-registry-read ""hkey_current_user\\software\\ghl3probe"" ""s"")",
                @"; error: bad registry key name: ""hkey_current_user\\software\\ghl3probe"""
            );
            Expect(
                lisp,
                @"(vl-registry-read ""HKCU\\Software\\GhL3Probe"" ""S"")",
                @"; error: bad registry key name: ""HKCU\\Software\\GhL3Probe"""
            );
            Expect(
                lisp,
                @"(vl-registry-write ""HKCU\\Software\\X"" ""a"" ""b"")",
                @"; error: bad registry key name: ""HKCU\\Software\\X"""
            );
        }

        [Fact]
        public void BelowTheRoot_NamesCompareIgnoringCase_AndKeepTheCaseTheyWereWrittenIn()
        {
            // ours: D117, as Windows does
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(lisp, @"(vl-registry-read ""HKEY_CURRENT_USER\\software\\ghl3probe"" ""s"")", "\"text\"");
            Expect(lisp, @"(vl-registry-write ""HKEY_CURRENT_USER\\SOFTWARE\\GHL3PROBE"" ""n"" 7)", "7");
            Expect(lisp, "(vl-registry-read " + Key + " \"N\")", "7");
            Expect(lisp, @"(vl-registry-descendents ""HKEY_CURRENT_USER\\Software"")", "(\"GhL3Probe\")");
            Expect(lisp, "(vl-registry-descendents " + Key + " T)", "(\"N\" \"S\")");
        }

        [Fact]
        public void Descendents_GiveTheSubKeysOrWithASecondArgumentTheValueNames()
        {
            // AutoCAD 2021 (r4): sub-keys ("Sub"); with T or "" the value names ("N" "S"), S written first
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(lisp, "(vl-registry-descendents " + Key + ")", "(\"Sub\")");
            Expect(lisp, "(vl-registry-descendents " + Key + " T)", "(\"N\" \"S\")");
            Expect(lisp, "(vl-registry-descendents " + Key + " \"\")", "(\"N\" \"S\")");
            Expect(lisp, "(vl-registry-descendents " + Sub + ")", "nil");
            Expect(lisp, @"(vl-registry-descendents ""HKEY_CURRENT_USER\\Software\\Nosuch"")", "nil");
        }

        [Fact]
        public void TheSubKeys_ComeInDescendingOrder_AndTheValuesNewestFirst()
        {
            // ours: D117 (r4 wrote S then N and gave ("N" "S"))
            var lisp = new LispInterpreter();
            lisp.Run(@"(vl-registry-write ""HKEY_CURRENT_USER\\Software\\GhL3Order\\b"" ""x"" 1)");
            lisp.Run(@"(vl-registry-write ""HKEY_CURRENT_USER\\Software\\GhL3Order\\C"" ""x"" 1)");
            lisp.Run(@"(vl-registry-write ""HKEY_CURRENT_USER\\Software\\GhL3Order\\a"" ""x"" 1)");
            lisp.Run(@"(vl-registry-write ""HKEY_CURRENT_USER\\Software\\GhL3Order"" ""zeta"" 1)");
            lisp.Run(@"(vl-registry-write ""HKEY_CURRENT_USER\\Software\\GhL3Order"" ""alpha"" 2)");

            Expect(
                lisp,
                @"(vl-registry-descendents ""HKEY_CURRENT_USER\\Software\\GhL3Order"")",
                "(\"C\" \"b\" \"a\")"
            );
            Expect(
                lisp,
                @"(vl-registry-descendents ""HKEY_CURRENT_USER\\Software\\GhL3Order"" T)",
                "(\"alpha\" \"zeta\")"
            );
        }

        [Fact]
        public void Delete_FollowsR4_AValueIsTAKeyWithSubKeysIsNil_AKeyWithoutIsRemoved()
        {
            // AutoCAD 2021 (r4): delete key "S" T, then read nil; delete the key (it has Sub) nil; delete Sub T; delete the key T,
            // then read "N" nil; delete a key that was never made T
            var lisp = new LispInterpreter();
            WriteProbeKeys(lisp);

            Expect(lisp, "(vl-registry-delete " + Key + " \"S\")", "T");
            Expect(lisp, "(vl-registry-read " + Key + " \"S\")", "nil");
            Expect(lisp, "(vl-registry-delete " + Key + ")", "nil");
            Expect(lisp, "(vl-registry-delete " + Sub + ")", "T");
            Expect(lisp, "(vl-registry-delete " + Key + ")", "T");
            Expect(lisp, "(vl-registry-read " + Key + " \"N\")", "nil");
            Expect(lisp, @"(vl-registry-delete ""HKEY_CURRENT_USER\\Software\\GhL3Probe2"")", "T");
        }

        [Fact]
        public void TheKeyAndNameAreAStringOrASymbol_AnythingElseIsATypeError()
        {
            // AutoCAD 2021 (r4): (vl-registry-write 5 "a" "b") ; error: bad argument type: (or stringp symbolp): 5
            var lisp = new LispInterpreter();

            Expect(lisp, "(vl-registry-write 5 \"a\" \"b\")", "; error: bad argument type: (or stringp symbolp): 5");
            Expect(lisp, "(vl-registry-read 5)", "; error: bad argument type: (or stringp symbolp): 5");
            Expect(lisp, "(vl-registry-descendents 5)", "; error: bad argument type: (or stringp symbolp): 5");
            Expect(lisp, "(vl-registry-delete 5)", "; error: bad argument type: (or stringp symbolp): 5");
            Expect(lisp, "(vl-registry-read " + Key + " 5)", "; error: bad argument type: (or stringp symbolp): 5");
            Expect(
                lisp,
                "(vl-registry-write " + Key + " 5 \"v\")",
                "; error: bad argument type: (or stringp symbolp): 5"
            );
        }

        [Fact]
        public void TwoInterpretersWithNoFile_DoNotShareTheirValues()
        {
            var one = new LispInterpreter();
            var two = new LispInterpreter();
            WriteProbeKeys(one);

            Expect(two, "(vl-registry-read " + Key + " \"S\")", "nil");
        }

        [Fact]
        public void TwoInterpretersWithTheSameFile_ShareTheirValues_ReadFreshBeforeEachRead()
        {
            var one = new LispInterpreter { RegistryFile = RegistryPath };
            var two = new LispInterpreter { RegistryFile = RegistryPath };
            WriteProbeKeys(one);

            Expect(two, "(vl-registry-read " + Key + " \"S\")", "\"text\"");
            Expect(two, "(vl-registry-read " + Sub + ")", "\"def\"");
            Expect(two, "(vl-registry-write " + Key + " \"S\" \"changed\")", "\"changed\"");
            Expect(one, "(vl-registry-read " + Key + " \"S\")", "\"changed\"");
            Expect(two, "(vl-registry-delete " + Key + " \"N\")", "T");
            Expect(one, "(vl-registry-descendents " + Key + " T)", "(\"S\")");
        }

        [Fact]
        public void AFileWithNoInterpreterYet_IsReadByANewOne()
        {
            WriteProbeKeys(new LispInterpreter { RegistryFile = RegistryPath });

            Expect(new LispInterpreter { RegistryFile = RegistryPath }, "(vl-registry-read " + Key + " \"N\")", "42");
        }

        [Fact]
        public void TheFile_HoldsOneLinePerValue_AndAKeyWithNoValuesIsALineWithNoNameOrKind()
        {
            // ours: D117, UTF-8 without a BOM, LF; the keys in order of their paths, the values in the order written
            var lisp = new LispInterpreter { RegistryFile = RegistryPath };
            WriteProbeKeys(lisp);

            string expected =
                "HKEY_CURRENT_USER\\\\Software\t\t\t\n"
                + "HKEY_CURRENT_USER\\\\Software\\\\GhL3Probe\tS\tS\ttext\n"
                + "HKEY_CURRENT_USER\\\\Software\\\\GhL3Probe\tN\tI\t42\n"
                + "HKEY_CURRENT_USER\\\\Software\\\\GhL3Probe\\\\Sub\t\tS\tdef\n";
            byte[] bytes = File.ReadAllBytes(RegistryPath);

            Assert.Equal(expected, new UTF8Encoding(false).GetString(bytes));
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.DoesNotContain((byte)'\r', bytes);
        }

        [Fact]
        public void TheFile_FollowsADelete()
        {
            var lisp = new LispInterpreter { RegistryFile = RegistryPath };
            WriteProbeKeys(lisp);

            lisp.Run("(vl-registry-delete " + Key + " \"S\")");
            string afterValue = File.ReadAllText(RegistryPath);
            Assert.DoesNotContain("\tS\tS\ttext", afterValue);
            Assert.Contains("GhL3Probe\tN\tI\t42\n", afterValue);

            lisp.Run("(vl-registry-delete " + Sub + ")");
            lisp.Run("(vl-registry-delete " + Key + ")");

            Assert.Equal("HKEY_CURRENT_USER\\\\Software\t\t\t\n", File.ReadAllText(RegistryPath));
        }

        [Fact]
        public void AValueWithATabALineFeedAndABackslash_RoundTripsThroughTheFile()
        {
            var one = new LispInterpreter { RegistryFile = RegistryPath };
            var two = new LispInterpreter { RegistryFile = RegistryPath };

            Expect(one, "(vl-registry-write " + Key + " \"t\\tn\" \"a\\tb\\nc\\\\d\\\\n\")", "\"a\\tb\\nc\\\\d\\\\n\"");

            Expect(two, "(vl-registry-read " + Key + " \"t\\tn\")", "\"a\\tb\\nc\\\\d\\\\n\"");
            Expect(two, "(vl-registry-descendents " + Key + " T)", "(\"t\\tn\")");

            string text = File.ReadAllText(RegistryPath);
            Assert.Contains("\tt\\tn\tS\ta\\tb\\nc\\\\d\\\\n\n", text);
        }

        [Fact]
        public void TheDefaultValueAndAnIntegerOfAnyKind_AreKept()
        {
            var one = new LispInterpreter { RegistryFile = RegistryPath };

            Expect(one, "(vl-registry-write " + Key + " \"\" \"def\")", "\"def\"");
            Expect(one, "(vl-registry-write " + Key + " \"neg\" -5)", "-5");
            Expect(one, "(vl-registry-write " + Key + " 'sym \"by symbol\")", "\"by symbol\"");

            var two = new LispInterpreter { RegistryFile = RegistryPath };
            Expect(two, "(vl-registry-read " + Key + ")", "\"def\"");
            Expect(two, "(vl-registry-read " + Key + " \"neg\")", "-5");
            Expect(two, "(vl-registry-read " + Key + " \"SYM\")", "\"by symbol\"");
        }

        [Fact]
        public void ReadingFromAFileThatIsNotThere_GivesNil_AndMakesNoFile()
        {
            var lisp = new LispInterpreter { RegistryFile = RegistryPath };

            Expect(lisp, "(vl-registry-read " + Key + " \"S\")", "nil");
            Expect(lisp, "(vl-registry-delete " + Key + ")", "T");
            Assert.False(File.Exists(RegistryPath));
        }

        [Fact]
        public void TheFile_IsMadeWithItsFolder()
        {
            string nested = Path.Combine(_folder, "made", "deeper", "registry.txt");
            var lisp = new LispInterpreter { RegistryFile = nested };

            Expect(lisp, "(vl-registry-write " + Key + " \"S\" \"x\")", "\"x\"");
            Assert.True(File.Exists(nested));
        }

        [Fact]
        public void ALineThatIsNotAValue_IsSkipped_AndTheRestIsRead()
        {
            File.WriteAllText(
                RegistryPath,
                "not a line\nHKCU\\\\bad\tx\tS\tv\nHKEY_CURRENT_USER\\\\Software\\\\GhL3Probe\tS\tS\ttext\nHKEY_CURRENT_USER\\\\Software\\\\GhL3Probe\tN\tI\tnotanumber\n",
                new UTF8Encoding(false)
            );
            var lisp = new LispInterpreter { RegistryFile = RegistryPath };

            Expect(lisp, "(vl-registry-read " + Key + " \"S\")", "\"text\"");
            Expect(lisp, "(vl-registry-read " + Key + " \"N\")", "nil");
        }
    }
}
