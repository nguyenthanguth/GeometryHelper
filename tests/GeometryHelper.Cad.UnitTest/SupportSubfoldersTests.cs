using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The support folders beside the app (Fonts, Hatches, Linetypes, Lisps, DrawingTemplates) are read with their subfolders,
    /// depth first: a folder's own files by name, then its subfolders in the same order. A file at the top comes before one in
    /// a subfolder, and of two files of one name the first wins. A junction (so that a loop cannot happen) and a hidden folder
    /// are not entered. The program folder is made under the temporary folder and removed after.
    /// </summary>
    public sealed class SupportSubfoldersTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-support-sub-" + Guid.NewGuid().ToString("N")
        );

        public SupportSubfoldersTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private string Program => Path.Combine(_root, "app");

        private string Write(string relative, string text)
        {
            string path = Path.Combine(Program, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        private string WriteBytes(string relative, byte[] bytes)
        {
            string path = Path.Combine(Program, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private CadDocument Document()
        {
            CadDocument document = CadDocument.From(
                new DxfDatabase("AC1027"),
                Path.Combine(_root, "drawing", "plan.dxf")
            );
            document.SupportRoots = new CadSupportFolders(Program);
            return document;
        }

        private CadFonts Fonts() =>
            new CadFonts(null, CadFonts.BuiltInName, null).WithRoots(new CadSupportFolders(Program));

        /// <summary>Makes a junction from a link to a target with mklink /J, which needs no administrator; false where it cannot.</summary>
        private static bool Junction(string link, string target)
        {
            try
            {
                var start = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (Process process = Process.Start(start))
                {
                    process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return process.ExitCode == 0 && Directory.Exists(link);
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        // ---- The walk

        [Fact]
        public void TheWalk_GoesDepthFirst_EachFolderBeforeItsSubfolders_InTheOrderOfTheirNames()
        {
            Directory.CreateDirectory(Path.Combine(Program, "Fonts", "b"));
            Directory.CreateDirectory(Path.Combine(Program, "Fonts", "A", "z"));
            Directory.CreateDirectory(Path.Combine(Program, "Fonts", "A", "Y"));
            Directory.CreateDirectory(Path.Combine(Program, "Fonts", "c"));
            string fonts = Path.Combine(Program, "Fonts");

            Assert.Equal(
                new[]
                {
                    fonts,
                    Path.Combine(fonts, "A"),
                    Path.Combine(fonts, "A", "Y"),
                    Path.Combine(fonts, "A", "z"),
                    Path.Combine(fonts, "b"),
                    Path.Combine(fonts, "c"),
                },
                new CadSupportFolders(Program).SearchFoldersOf(CadSupportKind.Fonts)
            );
        }

        [Fact]
        public void AFolderThatDoesNotExist_HasNoSearchFolders()
        {
            Assert.Empty(new CadSupportFolders(Program).SearchFoldersOf(CadSupportKind.Fonts));
        }

        [Fact]
        public void AJunctionBackToTheTop_IsNotEntered_SoTheWalkEnds()
        {
            string fonts = Path.Combine(Program, "Fonts");
            Directory.CreateDirectory(Path.Combine(fonts, "real"));

            if (!Junction(Path.Combine(fonts, "loop"), fonts))
            {
                // mklink /J is not available here (not Windows, or the volume refuses): the loop cannot be made.
                return;
            }

            Assert.Equal(
                new[] { fonts, Path.Combine(fonts, "real") },
                new CadSupportFolders(Program).SearchFoldersOf(CadSupportKind.Fonts)
            );
        }

        [Fact]
        public void AHiddenSubfolder_IsNotEntered_ButAVisibleOneIs()
        {
            string fonts = Path.Combine(Program, "Fonts");
            string hidden = Path.Combine(fonts, "hidden");
            Directory.CreateDirectory(hidden);
            Directory.CreateDirectory(Path.Combine(fonts, "shown"));
            new DirectoryInfo(hidden).Attributes |= FileAttributes.Hidden;

            Assert.Equal(
                new[] { fonts, Path.Combine(fonts, "shown") },
                new CadSupportFolders(Program).SearchFoldersOf(CadSupportKind.Fonts)
            );
        }

        // ---- Fonts

        [Fact]
        public void AShxFontInASubfolderOfFonts_IsFound()
        {
            string path = WriteBytes(Path.Combine("Fonts", "Company", "deep", "mine.shx"), ShxFiles.Unifont());

            Assert.Equal(path, Fonts().Locate("mine", null), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void OfTwoFontsOfOneName_TheOneAtTheTopWins_AndOtherwiseTheFirstDepthFirst()
        {
            string top = WriteBytes(Path.Combine("Fonts", "mine.shx"), ShxFiles.Unifont());
            WriteBytes(Path.Combine("Fonts", "a", "mine.shx"), ShxFiles.Unifont());
            WriteBytes(Path.Combine("Fonts", "a", "z", "other.shx"), ShxFiles.Unifont());
            string first = WriteBytes(Path.Combine("Fonts", "a", "z", "deep.shx"), ShxFiles.Unifont());
            WriteBytes(Path.Combine("Fonts", "b", "deep.shx"), ShxFiles.Unifont());

            CadFonts fonts = Fonts();

            Assert.Equal(top, fonts.Locate("mine", null), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(first, fonts.Locate("deep", null), StringComparer.OrdinalIgnoreCase);
        }

        // ---- Hatches and linetypes

        [Fact]
        public void APatternFileInASubfolderOfHatches_IsFound_ListedAfterTheTopOnes_AndOnTheSearchPath()
        {
            string top = Write(Path.Combine("Hatches", "b.pat"), "*B,top\n45, 0,0, 0,4\n");
            string deep = Write(Path.Combine("Hatches", "Acme", "a.pat"), "*A,deep\n45, 0,0, 0,4\n");
            CadDocument document = Document();

            Assert.Equal(deep, CadSupportFiles.Find(document, "a.pat"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(new[] { top, deep }, CadSupportFiles.List(document, ".pat"));
            Assert.Equal(
                new[] { Path.GetDirectoryName(top), Path.GetDirectoryName(deep) },
                document.PatternSearchPath.Split(';')
            );
        }

        [Fact]
        public void OfTwoPatternFilesOfOneName_TheOneAtTheTopWins()
        {
            string top = Write(Path.Combine("Hatches", "mine.pat"), "*MINE,top\n45, 0,0, 0,4\n");
            Write(Path.Combine("Hatches", "Old", "mine.pat"), "*MINE,old\n45, 0,0, 0,4\n");
            CadDocument document = Document();

            Assert.Equal(top, CadSupportFiles.Find(document, "mine.pat"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(top, Assert.Single(CadSupportFiles.List(document, ".pat")), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ALinetypeFileInASubfolderOfLinetypes_IsFound_AfterTheTopOnes_AndTheTopOneWinsADuplicate()
        {
            string zeta = Write(Path.Combine("Linetypes", "zeta.lin"), "*ZETA,top\nA,4,-1\n");
            string dupe = Write(Path.Combine("Linetypes", "dupe.lin"), "*DUPE,top\nA,4,-1\n");
            Write(Path.Combine("Linetypes", "Acme", "dupe.lin"), "*DUPE,deep\nA,4,-1\n");
            string deep = Write(Path.Combine("Linetypes", "Acme", "alpha.lin"), "*ALPHA,deep\nA,4,-1\n");
            CadDocument document = Document();

            Assert.Equal(deep, CadSupportFiles.Find(document, "alpha.lin"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(dupe, CadSupportFiles.Find(document, "dupe.lin"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(new[] { dupe, zeta, deep }, CadSupportFiles.List(document, ".lin"));
        }

        // ---- Lisps

        private void Mark(string relative) =>
            Write(Path.Combine("Lisps", relative), "(princ \"[" + relative.Replace('\\', '/') + "]\")");

        private CadCommandSession Opened()
        {
            CadCommandSession session = Session(Blank());
            session.Document.SupportRoots = new CadSupportFolders(Program);
            new LispStartup().OnDocumentOpened(session);
            return session;
        }

        [Fact]
        public void EveryLspAtAnyDepth_LoadsAtStart_TheTopOnesFirst_ThenEachSubfolderDepthFirst()
        {
            Mark("z.lsp");
            Mark(Path.Combine("b", "q.lsp"));
            Mark(Path.Combine("a", "z", "deep.lsp"));
            Mark(Path.Combine("a", "y.lsp"));
            Mark(Path.Combine("a", "a.lsp"));
            Mark("m.lsp");

            CadCommandSession session = Opened();

            Assert.Equal(
                new[] { "[m.lsp]", "[z.lsp]", "[a/a.lsp]", "[a/y.lsp]", "[a/z/deep.lsp]", "[b/q.lsp]" },
                session.History
            );
        }

        [Fact]
        public void AnAcadLspInASubfolderOfLisps_IsAnOrdinaryFileThere_ButNotAtTheTopLevel()
        {
            Mark("acad.lsp");
            Mark(Path.Combine("Sub", "acad.lsp"));

            CadCommandSession session = Opened();

            // The top acad.lsp is left to its own step (found along the search path); the one in Sub is loaded in its turn.
            Assert.Contains("[Sub/acad.lsp]", session.History);
        }

        [Fact]
        public void TheSearchPathOfLoad_StaysTheLispsFolderAlone()
        {
            Mark(Path.Combine("Sub", "x.lsp"));
            var roots = new CadSupportFolders(Program);

            Assert.Equal(new[] { Path.Combine(Program, "Lisps") }, roots.ExistingOf(CadSupportKind.Lisps));
        }

        // ---- Drawing templates

        [Fact]
        public void ATemplateInASubfolder_IsListedAfterTheTopOnes_AndFoundByItsName()
        {
            string folder = Path.Combine(Program, "DrawingTemplates");
            string top = Write(Path.Combine("DrawingTemplates", "b.dwt"), "x");
            string deep = Write(Path.Combine("DrawingTemplates", "Acme", "a.dwt"), "x");
            string deeper = Write(Path.Combine("DrawingTemplates", "Acme", "Old", "c.dxf"), "x");

            Assert.Equal(new[] { top, deep, deeper }, CadTemplate.FindIn(folder));
            Assert.Equal(deep, CadTemplate.Find("a", folder));
            Assert.Equal(deeper, CadTemplate.Find("c.dxf", folder));
            Assert.Null(CadTemplate.Find(Path.Combine("Acme", "none"), folder));
        }

        [Fact]
        public void OfTwoTemplatesOfOneName_TheOneAtTheTopWins()
        {
            string folder = Path.Combine(Program, "DrawingTemplates");
            string top = Write(Path.Combine("DrawingTemplates", "sheet.dwt"), "x");
            Write(Path.Combine("DrawingTemplates", "Old", "sheet.dwt"), "x");
            string other = Write(Path.Combine("DrawingTemplates", "Old", "sheet.dxf"), "x");

            Assert.Equal(new[] { top, other }, CadTemplate.FindIn(folder));
            Assert.Equal(top, CadTemplate.Find("sheet", folder));
        }
    }
}
