using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>findfile</c>, the <c>vl-filename-</c> functions, <c>vl-filename-mktemp</c>, <c>vl-file-size</c>,
    /// <c>vl-file-systime</c>, <c>vl-file-directory-p</c>, <c>vl-file-copy</c>, <c>vl-file-rename</c>,
    /// <c>vl-file-delete</c>, <c>vl-mkdir</c> and <c>vl-directory-files</c>, against AutoCAD 2021's accoreconsole (probes r4
    /// and r5 of the L3 survey, which ran with the drawing in one folder and the current folder in another). The folders are
    /// made under the temporary folder and removed after.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispFilenameTests : LispFolderTests
    {
        /// <summary>A string as <c>prin1</c> writes it: quoted, each backslash doubled.</summary>
        private static string Q(string text) => "\"" + text.Replace("\\", "\\\\") + "\"";

        private string Full(string relative) => Path.Combine(Folder, relative.Replace('/', '\\'));

        private LispInterpreter WithFolders(out string drawing, out string support)
        {
            drawing = Path.Combine(Folder, "dwg");
            support = Path.Combine(Folder, "sup");
            Directory.CreateDirectory(drawing);
            Directory.CreateDirectory(support);
            Make("dwg/ghbeside.txt", "62");
            Make("dwg/ghsame.txt", "64");
            Make("sup/ghsup.txt", "73");
            Make("sup/ghsame.txt", "73");
            Make("ghcwd.txt", "63 77 64 0d 0a");
            Make("ghsame.txt", "63");
            string dwg = drawing;
            string sup = support;
            LispInterpreter lisp = NewLisp();
            lisp.DrawingFolder = () => dwg + "\\";
            lisp.SupportFolders = new[] { sup };
            return lisp;
        }

        [Fact]
        public void Findfile_LooksInTheCurrentFolder_ThenTheDrawing_ThenTheSupportFolders()
        {
            LispInterpreter lisp = WithFolders(out string drawing, out string support);

            // AutoCAD 2021: (findfile "ghcwd.txt") the current folder; "ghbeside.txt" the drawing's folder (r4)
            Expect(lisp, "(findfile \"ghcwd.txt\")", Q(Full("ghcwd.txt")));
            Expect(lisp, "(findfile \"ghbeside.txt\")", Q(Path.Combine(drawing, "ghbeside.txt")));
            // AutoCAD 2021: a file of the same name in both: the current folder's (r5, ghsame.txt)
            Expect(lisp, "(findfile \"ghsame.txt\")", Q(Full("ghsame.txt")));
            // AutoCAD 2021: (findfile "acad.pgp") a file on the support path (r4)
            Expect(lisp, "(findfile \"ghsup.txt\")", Q(Path.Combine(support, "ghsup.txt")));
            // AutoCAD 2021: (findfile "nosuch.txt") nil (r4)
            Expect(lisp, "(findfile \"nosuch.txt\")", "nil");
        }

        [Fact]
        public void Findfile_TheDrawingFolderCameBeforeTheSupportFolders()
        {
            LispInterpreter lisp = WithFolders(out string drawing, out _);
            File.Delete(Full("ghsame.txt"));

            // AutoCAD 2021: the order is current folder, drawing folder, support path (r4, r5)
            Expect(lisp, "(findfile \"ghsame.txt\")", Q(Path.Combine(drawing, "ghsame.txt")));
        }

        [Fact]
        public void Findfile_ForADrawingNeverSaved_SkipsTheDrawingFolder()
        {
            LispInterpreter lisp = WithFolders(out _, out _);

            // ours: D113, none or "" for an unsaved drawing
            lisp.DrawingFolder = () => "";
            Expect(lisp, "(findfile \"ghbeside.txt\")", "nil");
            lisp.DrawingFolder = null;
            Expect(lisp, "(findfile \"ghbeside.txt\")", "nil");
            Expect(lisp, "(findfile \"ghsup.txt\")", Q(Full("sup/ghsup.txt")));
        }

        [Fact]
        public void Findfile_GivesTheFullPathWithBackslashes_ForAnyWayOfWritingIt()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: a forward-slash path and a relative one with a folder come back as D:\...\files\ansi.txt (r4)
            Expect(lisp, "(findfile (strcat D \"ansi.txt\"))", Q(Full("ansi.txt")));
            Expect(lisp, "(findfile \"dir/a.lsp\")", Q(Full("dir/a.lsp")));
            Expect(lisp, "(findfile \"dir\\\\a.lsp\")", Q(Full("dir/a.lsp")));
            // AutoCAD 2021: a folder is found too (r4)
            Expect(lisp, "(findfile \"dir\")", Q(Full("dir")));
            Expect(lisp, "(findfile (strcat D \"dir\"))", Q(Full("dir")));
        }

        [Fact]
        public void Findfile_RefusesANameEndingInASeparator_AndTheEmptyName()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (findfile "files\\dir\\") nil; (findfile "") nil (r4)
            Expect(lisp, "(findfile \"dir\\\\\")", "nil");
            Expect(lisp, "(findfile \"dir/\")", "nil");
            Expect(lisp, "(findfile \"\")", "nil");
            ExpectError(lisp, "(findfile 5)", "bad argument type: stringp 5"); // AutoCAD 2021 (r4)
        }

        [Theory]
        [InlineData("(vl-filename-base \"C:\\\\a\\\\b.c.txt\")", "\"b.c\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-base \"C:/a/b.txt\")", "\"b\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-base \"b\")", "\"b\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-base \"C:\\\\a\\\\\")", "\".\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-base \".txt\")", "\"\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-base \"\")", "\"\"")] // AutoCAD 2021: (r5)
        [InlineData("(vl-filename-base \"a/b\\\\c.d\")", "\"c\"")] // AutoCAD 2021: (r5)
        [InlineData("(vl-filename-directory \"C:\\\\a\\\\b.txt\")", "\"C:\\\\a\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"C:/a/b/c.txt\")", "\"C:/a/b\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"b.txt\")", "\"\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"C:\\\\a\\\\\")", "\"C:\\\\a\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"C:\\\\\")", "\"C:\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"C:b.txt\")", "\"C:\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-directory \"\")", "\"\"")] // AutoCAD 2021: (r5)
        [InlineData("(vl-filename-directory \"\\\\\\\\server\\\\share\\\\f.txt\")", "\"\\\\\\\\server\\\\share\"")] // AutoCAD 2021: (r5)
        [InlineData("(vl-filename-extension \"C:\\\\a\\\\b.c.txt\")", "\".txt\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-extension \"C:\\\\a.d\\\\b\")", "nil")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-extension \"b.\")", "\".\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-extension \".txt\")", "\".txt\"")] // AutoCAD 2021: (r4)
        [InlineData("(vl-filename-extension \"\")", "nil")] // AutoCAD 2021: (r5)
        public void TheFileNameFunctions_WorkOnTheTextAlone(string form, string output)
        {
            Expect(new LispInterpreter(), form, output);
        }

        [Fact]
        public void TheFileNameFunctions_RefuseWhatIsNotAString()
        {
            var lisp = new LispInterpreter();

            // AutoCAD 2021: (vl-filename-extension 5) ; error: bad argument type: stringp 5 (r4)
            ExpectError(lisp, "(vl-filename-extension 5)", "bad argument type: stringp 5");
            ExpectError(lisp, "(vl-filename-base 5)", "bad argument type: stringp 5");
            ExpectError(lisp, "(vl-filename-directory 5)", "bad argument type: stringp 5");
        }

        [Fact]
        public void Mktemp_TakesFiveCharactersAndACounter_AndCreatesNothing()
        {
            LispInterpreter lisp = NewLisp();
            string folder = D.TrimEnd('/');
            string[] before = Directory.GetFileSystemEntries(Folder);

            // AutoCAD 2021: ".../files\\ab004.x" ... the counter runs on, the name cut to five, the extension of the
            // argument over the pattern's (r4; the counter itself starts at 1 here, D115)
            Expect(lisp, "(vl-filename-mktemp \"ab\" D \".x\")", Q(folder + "\\ab001.x"));
            Expect(lisp, "(vl-filename-mktemp \"ab\" D \".x\")", Q(folder + "\\ab002.x"));
            Expect(lisp, "(vl-filename-mktemp \"abcdefghij.dcl\" D)", Q(folder + "\\abcde003.dcl"));
            Expect(lisp, "(vl-filename-mktemp \"ab$$\" D)", Q(folder + "\\ab$$004"));
            Expect(lisp, "(vl-filename-mktemp (strcat D \"q.txt\"))", Q(folder + "\\q005.txt"));
            Expect(lisp, "(vl-filename-mktemp \"a.b\" D \".c\")", Q(folder + "\\a006.c"));
            Expect(lisp, "(vl-filename-mktemp \"ab\" (strcat D \"dir\\\\\") \".x\")", Q(folder + "/dir\\ab007.x"));
            Assert.Equal(before, Directory.GetFileSystemEntries(Folder));
        }

        [Fact]
        public void Mktemp_WithNoFolder_UsesTheTemporaryFolder_AndTheDefaultPattern()
        {
            LispInterpreter lisp = NewLisp();
            string temp = Path.GetTempPath().TrimEnd('\\', '/');

            // AutoCAD 2021: "C:\\Users\\...\\Temp\\$VL~~001", "...\\EditT002.dcl", "...\\a008.c" (r4)
            string plain = (string)lisp.Run("(vl-filename-mktemp)").Value;
            string dcl = (string)lisp.Run("(vl-filename-mktemp \"EditText.dcl\")").Value;
            string extension = (string)lisp.Run("(vl-filename-mktemp \"a.b\" nil \".c\")").Value;

            Assert.Matches("^" + Regex.Escape(temp + "\\$VL~~") + "[0-9]{3}$", plain);
            Assert.Matches("^" + Regex.Escape(temp + "\\EditT") + "[0-9]{3}\\.dcl$", dcl);
            Assert.Matches("^" + Regex.Escape(temp + "\\a") + "[0-9]{3}\\.c$", extension);
        }

        [Fact]
        public void Mktemp_SkipsNamesThatExist()
        {
            Make("zz001.x", "");
            Make("zz002.x", "");
            LispInterpreter lisp = NewLisp();
            string folder = D.TrimEnd('/');

            // AutoCAD 2021: with zz001.x and zz002.x there, the first call gave zz003 and the next zz004, zz005 (r5)
            Expect(lisp, "(vl-filename-mktemp \"zz\" D \".x\")", Q(folder + "\\zz003.x"));
            Expect(lisp, "(vl-filename-mktemp \"zz\" D \".x\")", Q(folder + "\\zz004.x"));
            Expect(lisp, "(vl-filename-mktemp \"zz\" D \".x\")", Q(folder + "\\zz005.x"));
        }

        [Fact]
        public void TheMktempCounter_BelongsToTheInterpreter()
        {
            // ours: D115
            Expect(NewLisp(), "(vl-filename-mktemp \"ab\" D \".x\")", Q(D.TrimEnd('/') + "\\ab001.x"));
            Expect(NewLisp(), "(vl-filename-mktemp \"ab\" D \".x\")", Q(D.TrimEnd('/') + "\\ab001.x"));
        }

        [Fact]
        public void FileSize_IsTheSize_ZeroForAFolder_AndNilWhenMissing()
        {
            LispInterpreter lisp = WithFolders(out _, out _);

            // AutoCAD 2021: 3, 0 for an empty file, 0 for a folder, nil for a missing one, 5 for a relative name (r4)
            Expect(lisp, "(vl-file-size (strcat D \"dir/a.lsp\"))", "3");
            Expect(lisp, "(vl-file-size (strcat D \"dir/C.TXT\"))", "0");
            Expect(lisp, "(vl-file-size (strcat D \"dir\"))", "0");
            Expect(lisp, "(vl-file-size (strcat D \"nosuch\"))", "nil");
            Expect(lisp, "(vl-file-size \"ghcwd.txt\")", "5");
        }

        [Fact]
        public void FileSystime_IsALocalTimeOfEightNumbers_NilForAFolderOrAMissingFile()
        {
            LispInterpreter lisp = NewLisp();
            File.SetLastWriteTime(Full("dir/a.lsp"), new DateTime(2026, 11, 21, 10, 28, 11, 952, DateTimeKind.Local));

            // AutoCAD 2021: (2026 11 6 21 10 28 11 952) for a file written on Saturday 21 November (r5)
            Expect(lisp, "(vl-file-systime (strcat D \"dir/a.lsp\"))", "(2026 11 6 21 10 28 11 952)");
            Expect(lisp, "(length (vl-file-systime (strcat D \"dir/a.lsp\")))", "8");
            Expect(lisp, "(type (vl-file-systime (strcat D \"dir/a.lsp\")))", "LIST");
            Expect(lisp, "(vl-file-systime (strcat D \"nosuch\"))", "nil");
            Expect(lisp, "(vl-file-systime (strcat D \"dir\"))", "nil");
        }

        [Theory]
        [InlineData("(vl-file-directory-p (strcat D \"dir\"))", "T")] // AutoCAD 2021: (r4)
        [InlineData("(vl-file-directory-p (strcat D \"dir/\"))", "T")] // AutoCAD 2021: (r4)
        [InlineData("(vl-file-directory-p (strcat D \"dir/a.lsp\"))", "nil")] // AutoCAD 2021: (r4)
        [InlineData("(vl-file-directory-p (strcat D \"nosuch\"))", "nil")] // AutoCAD 2021: (r4)
        [InlineData("(vl-file-directory-p \"dir\")", "T")] // AutoCAD 2021: a relative name (r4)
        public void FileDirectoryP_IsTrueForAFolder(string form, string output)
        {
            Expect(NewLisp(), form, output);
        }

        [Theory]
        [InlineData(
            "(vl-directory-files (strcat D \"dir\"))",
            "(\".\" \"..\" \"a.lsp\" \"b.txt\" \"C.TXT\" \"sub1\" \"sub2\")"
        )] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") \"*.txt\")", "(\"b.txt\" \"C.TXT\")")] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") nil 1)", "(\"a.lsp\" \"b.txt\" \"C.TXT\")")] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") nil -1)", "(\".\" \"..\" \"sub1\" \"sub2\")")] // AutoCAD 2021: (r4)
        [InlineData(
            "(vl-directory-files (strcat D \"dir\") \"*.*\" 0)",
            "(\".\" \"..\" \"a.lsp\" \"b.txt\" \"C.TXT\" \"sub1\" \"sub2\")"
        )] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") \"*.TXT\" 1)", "(\"b.txt\" \"C.TXT\")")] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"nosuch\"))", "nil")] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") \"*.zzz\")", "nil")] // AutoCAD 2021: (r4)
        [InlineData("(vl-directory-files \"dir\" \"*.lsp\")", "(\"a.lsp\")")] // AutoCAD 2021: a relative folder (r4)
        [InlineData("(vl-directory-files (strcat D \"dir\") \"?.txt\")", "(\"b.txt\" \"C.TXT\")")] // ours: D116, ? is one character
        [InlineData("(vl-directory-files (strcat D \"dir\") \"s*\" -1)", "(\"sub1\" \"sub2\")")] // ours: D116, "." and ".." only where the pattern fits them
        public void DirectoryFiles_ListsFilesAndFolders_InTheOrderOfTheirNames(string form, string output)
        {
            Expect(NewLisp(), form, output);
        }

        [Fact]
        public void DirectoryFiles_WithNoFolder_ListsTheCurrentFolder()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: (length (vl-directory-files)) 32, the current folder (r4); here the fixture folder
            Expect(lisp, "(car (vl-directory-files))", "\".\"");
            Expect(lisp, "(vl-directory-files nil \"*.lsp\")", "nil");
            Expect(lisp, "(car (vl-directory-files nil \"a-*\" 1))", "\"a-ansi.txt\"");
            Expect(lisp, "(length (vl-directory-files nil nil 1))", "14");
        }

        [Fact]
        public void DirectoryFiles_RefusesANameThatIsNoString()
        {
            // AutoCAD 2021: (vl-directory-files dir 5) ; error: bad argument type: stringp 5 (r4)
            ExpectError(NewLisp(), "(vl-directory-files (strcat D \"dir\") 5)", "bad argument type: stringp 5");
            ExpectError(NewLisp(), "(vl-directory-files 5)", "bad argument type: stringp 5");
        }

        [Fact]
        public void Mkdir_MakesAFolder_AndSaysTWhenItIsThere_AndNilWhenTheParentIsMissing()
        {
            LispInterpreter lisp = NewLisp();

            // AutoCAD 2021: T, T, nil (r4)
            Expect(lisp, "(vl-mkdir (strcat D \"made\"))", "T");
            Assert.True(Directory.Exists(Full("made")));
            Expect(lisp, "(vl-mkdir (strcat D \"made\"))", "T");
            Expect(lisp, "(vl-mkdir (strcat D \"x/y\"))", "nil");
            Assert.False(Directory.Exists(Full("x")));
            // ours: D116, a relative name from the current folder, a trailing separator, a name that is a file
            Expect(lisp, "(vl-mkdir \"rel-made/\")", "T");
            Assert.True(Directory.Exists(Full("rel-made")));
            Expect(lisp, "(vl-mkdir (strcat D \"ansi.txt\"))", "nil");
        }

        [Fact]
        public void FileCopy_GivesTheBytesCopied_AndRefusesAnExistingTarget_UnlessAppending()
        {
            LispInterpreter lisp = NewLisp();
            Directory.CreateDirectory(Full("made"));

            // AutoCAD 2021: 3, nil, 3 (appended, size 6), nil for a missing source, nil for a folder as target (r4)
            Expect(lisp, "(vl-file-copy (strcat D \"dir/a.lsp\") (strcat D \"made/copy.lsp\"))", "3");
            Expect(lisp, "(vl-file-copy (strcat D \"dir/a.lsp\") (strcat D \"made/copy.lsp\"))", "nil");
            Expect(lisp, "(vl-file-copy (strcat D \"dir/a.lsp\") (strcat D \"made/copy.lsp\") T)", "3");
            Expect(lisp, "(vl-file-size (strcat D \"made/copy.lsp\"))", "6");
            Expect(lisp, "(vl-file-copy (strcat D \"nosuch\") (strcat D \"made/n.lsp\"))", "nil");
            Expect(lisp, "(vl-file-copy (strcat D \"dir/b.txt\") (strcat D \"made\"))", "nil");
            Assert.Equal("61 62 63 61 62 63", Bytes("made/copy.lsp"));
        }

        [Fact]
        public void FileRename_ReplacesAnExistingTarget_AndIsNilForAMissingSource()
        {
            LispInterpreter lisp = NewLisp();
            Directory.CreateDirectory(Full("made"));
            lisp.Run("(vl-file-copy (strcat D \"dir/a.lsp\") (strcat D \"made/copy.lsp\"))");
            lisp.Run("(vl-file-copy (strcat D \"dir/a.lsp\") (strcat D \"made/copy.lsp\") T)");

            // AutoCAD 2021: T, T (onto dir/a.lsp, replacing it), nil (r4)
            Expect(lisp, "(vl-file-rename (strcat D \"made/copy.lsp\") (strcat D \"made/ren.lsp\"))", "T");
            Expect(lisp, "(vl-file-rename (strcat D \"made/ren.lsp\") (strcat D \"dir/a.lsp\"))", "T");
            Expect(lisp, "(vl-file-rename (strcat D \"nosuch\") (strcat D \"made/x\"))", "nil");
            Assert.Equal("61 62 63 61 62 63", Bytes("dir/a.lsp"));
            Assert.False(File.Exists(Full("made/ren.lsp")));
            // ours: D116, a file renamed to itself stays; a missing folder for the target is nil and loses nothing
            Expect(lisp, "(vl-file-rename (strcat D \"dir/a.lsp\") (strcat D \"dir/A.LSP\"))", "T");
            Assert.True(File.Exists(Full("dir/a.lsp")));
            Expect(lisp, "(vl-file-rename (strcat D \"dir/a.lsp\") (strcat D \"nofolder/a.lsp\"))", "nil");
            Assert.True(File.Exists(Full("dir/a.lsp")));
        }

        [Fact]
        public void FileDelete_RemovesAFileOrAnEmptyFolder_AndIsNilWhenMissing()
        {
            LispInterpreter lisp = NewLisp();
            Directory.CreateDirectory(Full("made"));

            // AutoCAD 2021: nil for a file that is not there; T for the empty folder (r4)
            Expect(lisp, "(vl-file-delete (strcat D \"made/ren.lsp\"))", "nil");
            Expect(lisp, "(vl-file-delete (strcat D \"made\"))", "T");
            Assert.False(Directory.Exists(Full("made")));
            ExpectError(lisp, "(vl-file-delete 5)", "bad argument type: stringp 5"); // AutoCAD 2021 (r4)
            // r5: (vl-file-delete (strcat D "zz001.x")) T for a file
            Make("zz001.x", "");
            Expect(lisp, "(vl-file-delete (strcat D \"zz001.x\"))", "T");
            Assert.False(File.Exists(Full("zz001.x")));
            // ours: D116, a folder with files in it stays and gives nil
            Expect(lisp, "(vl-file-delete (strcat D \"dir\"))", "nil");
            Assert.True(File.Exists(Full("dir/a.lsp")));
        }
    }
}
