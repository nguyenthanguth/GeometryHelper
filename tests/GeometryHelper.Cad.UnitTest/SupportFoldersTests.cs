using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The default support folders beside GeometryHelper.Cad.dll (Fonts, Hatches, Linetypes): fonts, pattern files and
    /// linetype files looked for in the user's folders, then those folders, then the built-in sets, never in the drawing's
    /// folder; a TrueType font file found by its family; and the shipped linetype and pattern files kept equal to the
    /// built-in sets. The roots are injected: nothing is written beside the real dll.
    /// </summary>
    public sealed class SupportFoldersTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gh-support-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private string Folder(params string[] parts)
        {
            string path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Write(string folder, string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static string Write(string folder, string name, byte[] bytes)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static CadDocument Document(string drawingFolder, CadSupportFolders roots, params string[] support)
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), Path.Combine(drawingFolder, "plan.dxf"));
            document.SupportRoots = roots;
            document.SupportFolders = support;
            return document;
        }

        // ---- Fonts

        [Fact]
        public void AShxFontInTheFontsFolderNextToTheDll_IsFound()
        {
            string program = Folder("dll");
            string path = Write(Folder("dll", "Fonts"), "mine.shx", ShxFiles.Unifont());
            CadFonts fonts = new CadFonts(null, CadFonts.BuiltInName, null).WithRoots(new CadSupportFolders(program));

            Assert.Equal(path, fonts.Locate("mine", null), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(path, fonts.Locate("MINE.SHX", null), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AFontInTheUsersFolder_WinsOverTheSameNameNextToTheDll()
        {
            string program = Folder("dll");
            Write(Folder("dll", "Fonts"), "mine.shx", ShxFiles.Unifont());
            string users = Write(Folder("users"), "mine.shx", ShxFiles.Unifont());
            CadFonts fonts = new CadFonts(new[] { Path.GetDirectoryName(users) }, CadFonts.BuiltInName, null).WithRoots(
                new CadSupportFolders(program)
            );

            Assert.Equal(users, fonts.Locate("mine", null), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AFontNextToTheDll_BeatsTheBuiltInFont_AndTheBuiltInFontIsTheLastResort()
        {
            string program = Folder("dll");
            CadFonts none = new CadFonts(null, CadFonts.BuiltInName, null).WithRoots(new CadSupportFolders(program));

            Assert.Same(CadFonts.BuiltIn, none.FaceOf("romans.shx", null, null, null).Shx);

            Write(Folder("dll", "Fonts"), "romans.shx", ShxFiles.Unifont());
            CadFonts found = new CadFonts(null, CadFonts.BuiltInName, null).WithRoots(new CadSupportFolders(program));
            CadFontFace face = found.FaceOf("romans.shx", null, null, null);

            Assert.NotNull(face.Shx);
            Assert.NotSame(CadFonts.BuiltIn, face.Shx);
        }

        [Fact]
        public void AFolderThatDoesNotExist_IsSkippedWithoutAWord()
        {
            string missing = Path.Combine(_root, "nowhere");
            CadFonts fonts = new CadFonts(
                new[] { Path.Combine(missing, "users") },
                CadFonts.BuiltInName,
                null
            ).WithRoots(new CadSupportFolders(missing));

            Assert.Null(fonts.Locate("mine", null));
            Assert.Null(new CadFonts(null, null, null).WithRoots(new CadSupportFolders(null)).Locate("mine", null));
            Assert.Empty(new CadSupportFolders(missing).ExistingOf(CadSupportKind.Fonts));
            Assert.Equal(Path.Combine(missing, "Fonts"), new CadSupportFolders(missing).FolderOf(CadSupportKind.Fonts));
        }

        [Fact]
        public void AFontInTheDrawingsFolder_IsNotLookedFor()
        {
            string program = Folder("dll");
            string drawing = Folder("drawing");
            Write(drawing, "mine.shx", ShxFiles.Unifont());
            CadFonts fonts = new CadFonts(null, CadFonts.BuiltInName, null).WithRoots(new CadSupportFolders(program));

            Assert.Null(fonts.Locate("mine", drawing));
        }

        [Fact]
        public void ATrueTypeFileInTheFontsFolder_IsDrawnByItsFamily_WhenWindowsHasNotThatFamily()
        {
            string program = Folder("dll");
            string fontsFolder = Folder("dll", "Fonts");
            string path = Write(fontsFolder, "zork.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(), "Zork Sans"));
            var roots = new CadSupportFolders(program);
            CadFonts fonts = new CadFonts(null, CadFonts.BuiltInName, null, family => false).WithRoots(roots);

            CadFontFace face = fonts.FaceOf(null, null, "zork sans", null);

            Assert.Equal(path, face.Name, StringComparer.OrdinalIgnoreCase);
            Assert.Null(face.Shx);
        }

        [Fact]
        public void ATypefaceWindowsHas_IsFoundFirst_TheFileOfTheSameFamilyIsNotUsed()
        {
            string program = Folder("dll");
            Write(Folder("dll", "Fonts"), "zork.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(), "Zork Sans"));
            CadFonts fonts = new CadFonts(null, CadFonts.BuiltInName, null, family => family == "Zork Sans").WithRoots(
                new CadSupportFolders(program)
            );

            Assert.Equal("Zork Sans", fonts.FaceOf(null, null, "Zork Sans", null).Name);
        }

        [Fact]
        public void ATrueTypeFileInTheFontsFolder_IsNotListedAsMissing_AnAbsentOneIs()
        {
            string program = Folder("dll");
            string fontsFolder = Folder("dll", "Fonts");
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable["Standard"].FileName = CadFonts.BuiltInName + ".shx";
            database.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "Zork",
                    Font = new DxfFontDescriptor("Zork Sans", false, false, 0, 34),
                }
            );
            CadFonts Make() =>
                new CadFonts(null, CadFonts.BuiltInName, null, family => false).WithRoots(
                    new CadSupportFolders(program)
                );

            Assert.Equal(new[] { "Zork Sans" }, Make().MissingTrueType(database, null));

            string path = Write(fontsFolder, "zork.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(), "Zork Sans"));
            Assert.Empty(Make().MissingTrueType(database, null));

            // The folder's list of files is read again: a file taken away is missing once more.
            File.Delete(path);
            Assert.Equal(new[] { "Zork Sans" }, Make().MissingTrueType(database, null));
        }

        [Fact]
        public void TheRegularFileOfAFamily_IsChosenOverItsBoldOne()
        {
            string program = Folder("dll");
            string fontsFolder = Folder("dll", "Fonts");
            Write(fontsFolder, "a-bold.ttf", TrueTypeFile.Font(TrueTypeFile.Letters(), "Zork Sans", 1));
            string regular = Write(
                fontsFolder,
                "b-regular.ttf",
                TrueTypeFile.Font(TrueTypeFile.Letters(), "Zork Sans")
            );
            CadFonts fonts = new CadFonts(null, CadFonts.BuiltInName, null, family => false).WithRoots(
                new CadSupportFolders(program)
            );

            Assert.Equal(regular, fonts.FaceOf(null, null, "Zork Sans", null).Name, StringComparer.OrdinalIgnoreCase);
        }

        // ---- Linetype and pattern files

        [Fact]
        public void ALinFileAndAPatFileInTheDefaultFolders_AreFound_ByTheirNames()
        {
            string program = Folder("dll");
            string lin = Write(Folder("dll", "Linetypes"), "mine.lin", "*MINE,Mine\nA,4,-1\n");
            string pat = Write(Folder("dll", "Hatches"), "mine.pat", "*MINE,Mine\n45, 0,0, 0,4\n");
            CadDocument document = Document(Folder("drawing"), new CadSupportFolders(program));

            Assert.Equal(lin, CadSupportFiles.Find(document, "mine.lin"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(pat, CadSupportFiles.Find(document, "mine.pat"), StringComparer.OrdinalIgnoreCase);
            Assert.Null(CadSupportFiles.Find(document, "mine.pat.lin"));
            Assert.Contains(Path.GetDirectoryName(pat), document.PatternSearchPath.Split(';'));
        }

        [Fact]
        public void TheOrderOfALinFile_IsTheUsersFoldersThenTheOneNextToTheDll()
        {
            string program = Folder("dll");
            Write(Folder("dll", "Linetypes"), "mine.lin", "*MINE,dll\nA,4,-1\n");
            string users = Write(Folder("users"), "mine.lin", "*MINE,users\nA,4,-1\n");
            CadDocument document = Document(
                Folder("drawing"),
                new CadSupportFolders(program),
                Path.GetDirectoryName(users)
            );

            Assert.Equal(users, CadSupportFiles.Find(document, "mine.lin"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(
                users,
                Assert.Single(CadSupportFiles.List(document, ".lin")),
                StringComparer.OrdinalIgnoreCase
            );
        }

        [Fact]
        public void ALinFileAndAPatFileInTheDrawingsFolder_AreNotLookedFor()
        {
            string drawing = Folder("drawing");
            Write(drawing, "mine.lin", "*MINE,drawing\nA,4,-1\n");
            Write(drawing, "mine.pat", "*MINE,drawing\n45, 0,0, 0,4\n");
            CadDocument document = Document(drawing, new CadSupportFolders(Folder("dll")));

            Assert.Null(CadSupportFiles.Find(document, "mine.lin"));
            Assert.Null(CadSupportFiles.Find(document, "mine.pat"));
            Assert.Empty(CadSupportFiles.List(document, ".lin"));
            Assert.DoesNotContain(drawing, document.PatternSearchPath.Split(';'));
        }

        [Fact]
        public void ADefaultFolderThatDoesNotExist_IsSkippedByTheLookupOfSupportFiles()
        {
            CadDocument document = Document(Folder("drawing"), new CadSupportFolders(Path.Combine(_root, "nowhere")));

            Assert.Null(CadSupportFiles.Find(document, "mine.lin"));
            Assert.Empty(CadSupportFiles.List(document, ".pat"));
            Assert.Equal(string.Empty, document.PatternSearchPath);
        }

        // ---- What ships

        [Fact]
        public void TheShippedFolders_AreBesideTheDll_WithTheirReadmes()
        {
            CadSupportFolders folders = CadSupportFolders.Default;

            Assert.True(File.Exists(Path.Combine(folders.ProgramFolder, "GeometryHelper.Cad.dll")));

            foreach (
                CadSupportKind kind in new[] { CadSupportKind.Fonts, CadSupportKind.Hatches, CadSupportKind.Linetypes }
            )
            {
                Assert.Single(folders.ExistingOf(kind));
                Assert.True(
                    File.Exists(Path.Combine(folders.FolderOf(kind), "README.txt")),
                    kind + " has no README.txt."
                );
            }
        }

        [Fact]
        public void TheShippedLinFile_EqualsTheBuiltInLinetypes_NameDescriptionAndPattern()
        {
            string path = Path.Combine(
                CadSupportFolders.Default.FolderOf(CadSupportKind.Linetypes),
                "GeometryHelper.lin"
            );

            var shipped = CadLinetypeFile.Read(path);

            Assert.Equal(CadLinetypeFile.BuiltIn.Count, shipped.Count);

            for (int i = 0; i < shipped.Count; i++)
            {
                Assert.Null(shipped[i].Fault);
                Assert.Equal(CadLinetypeFile.BuiltIn[i].Name, shipped[i].Name);
                Assert.Equal(CadLinetypeFile.BuiltIn[i].Description, shipped[i].Description);
                Assert.Equal(CadLinetypeFile.BuiltIn[i].PatternText, shipped[i].PatternText);
            }
        }

        [Fact]
        public void TheShippedPatFile_EqualsTheBuiltInPatterns_NameDescriptionAndEveryLine()
        {
            string path = Path.Combine(
                CadSupportFolders.Default.FolderOf(CadSupportKind.Hatches),
                "GeometryHelper.pat"
            );

            var shipped = CadPatternFile.Read(path);

            Assert.Equal(CadPatternFile.BuiltIn.Count, shipped.Count);

            for (int i = 0; i < shipped.Count; i++)
            {
                CadHatchPattern built = CadPatternFile.BuiltIn[i];
                Assert.Equal(built.Name, shipped[i].Name);
                Assert.Equal(built.Description, shipped[i].Description);
                Assert.Equal(built.Lines.Count, shipped[i].Lines.Count);

                for (int k = 0; k < built.Lines.Count; k++)
                {
                    CadHatchPatternLine a = built.Lines[k];
                    CadHatchPatternLine b = shipped[i].Lines[k];
                    Assert.Equal(
                        new[] { a.Angle, a.BaseX, a.BaseY, a.Along, a.Across }.Concat(a.Dashes).ToArray(),
                        new[] { b.Angle, b.BaseX, b.BaseY, b.Along, b.Across }.Concat(b.Dashes).ToArray()
                    );
                }
            }
        }

        /// <summary>
        /// To write the file again after a change to HersheySimplex: set GH_WRITE_GHSIMPLEX to the full path of
        /// src\GeometryHelper.Cad\Fonts\ghsimplex.shx and run this test once; it writes the bytes there and still compares the
        /// copy beside the dll, which the next build refreshes.
        /// </summary>
        [Fact]
        public void TheShippedShxFile_EqualsTheBytesOfTheBuiltInFont()
        {
            byte[] built = HersheySimplex.Unifont();
            string target = Environment.GetEnvironmentVariable("GH_WRITE_GHSIMPLEX");

            if (!string.IsNullOrWhiteSpace(target))
            {
                File.WriteAllBytes(target, built);
            }

            string path = Path.Combine(CadSupportFolders.Default.FolderOf(CadSupportKind.Fonts), "ghsimplex.shx");

            Assert.True(File.Exists(path), "ghsimplex.shx is not in the Fonts folder.");
            Assert.Equal(built, File.ReadAllBytes(path));
        }
    }
}
