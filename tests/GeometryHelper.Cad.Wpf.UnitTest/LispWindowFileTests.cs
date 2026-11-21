using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// What the window does for the files functions of AutoLISP: the registry file of every drawing, <c>getfiled</c> answered
    /// by the window's file dialog, a line typed for <c>read-line</c> with its spaces, and the files a routine left open
    /// closed when its drawing is closed or read again. Driven with no window; the dialog itself is never shown.
    /// </summary>
    public sealed class LispWindowFileTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private MainViewModel Opened()
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            Assert.True(model.Open(room));
            return model;
        }

        // A path as a routine writes it in a string: forward slashes, which need no escape.
        private static string Slashed(string path) => path.Replace('\\', '/');

        #region The registry file

        [Fact]
        public void ADrawingOpened_KeepsTheLispRegistryBesideTheSettings_AndWritesNothingYet()
        {
            MainViewModel model = Opened();
            string expected = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(model.SettingsPath)),
                "lisp-registry.txt"
            );

            Assert.Equal(expected, model.Session.Lisp.RegistryFile);

            // ours: D117, the values live in a file of the presenter's own and no read of them makes it
            Assert.False(File.Exists(expected));
        }

        #endregion

        #region getfiled

        [Fact]
        public void Getfiled_WithAWindowThatChoosesAFile_GivesThePathAndHandsOverTheRequest()
        {
            MainViewModel model = Opened();
            CadGetFileRequest seen = null;
            model.AskGetFile = request =>
            {
                seen = request;
                return "C:\\Plans\\house.dwg";
            };

            model.Execute("(getfiled \"Open plan\" \"C:/Plans/\" \"dwg;dxf\" 8)");

            Assert.Equal("\"C:\\\\Plans\\\\house.dwg\"", model.History.Last());
            Assert.NotNull(seen);
            Assert.Equal("Open plan", seen.Title);
            Assert.Equal("C:/Plans/", seen.DefaultName);
            Assert.Equal("dwg;dxf", seen.Extension);
            Assert.Equal(8, seen.Flags);
        }

        [Fact]
        public void Getfiled_WithADialogTheUserCancels_GivesNil()
        {
            MainViewModel model = Opened();
            model.AskGetFile = request => null;

            model.Execute("(getfiled \"Open\" \"\" \"txt\" 0)");

            Assert.Equal("nil", model.History.Last());
        }

        [Fact]
        public void Getfiled_WithNoDialogSet_GivesNil()
        {
            // AutoCAD 2021 (r6): the console shows no dialog and gives nil
            MainViewModel model = Opened();

            model.Execute("(getfiled \"Open\" \"\" \"txt\" 0)");

            Assert.Equal("nil", model.History.Last());
        }

        #endregion

        #region The filter and the start of the dialog

        [Theory]
        [InlineData("dwg;dxf", 0, "dwg, dxf files (*.dwg;*.dxf)|*.dwg;*.dxf")]
        [InlineData("txt", 4, "txt files (*.txt)|*.txt|All files (*.*)|*.*")]
        [InlineData("", 0, "All files (*.*)|*.*")]
        [InlineData("*.lsp; .FAS;lsp", 0, "lsp, FAS files (*.lsp;*.FAS)|*.lsp;*.FAS")]
        public void TheFilter_IsTheExtensionsTheRoutineGave_WithAllFilesWhereNoneOrFlag4(
            string extension,
            int flags,
            string expected
        )
        {
            var request = new CadGetFileRequest("T", string.Empty, extension, flags);

            Assert.Equal(expected, CadGetFileDialog.FilterOf(request));
        }

        [Fact]
        public void TheStart_OfAFileInAFolderThatExists_IsThatFolderAndThatName()
        {
            string path = Path.Combine(_folder.FullPath, "new.txt");

            CadGetFileDialog.StartOf(new CadGetFileRequest("T", path, "txt", 1), out string folder, out string name);

            Assert.Equal(_folder.FullPath, folder);
            Assert.Equal("new.txt", name);
        }

        [Fact]
        public void TheStart_OfAFolder_OrWithFlag16_IsTheFolderAlone()
        {
            CadGetFileDialog.StartOf(
                new CadGetFileRequest("T", _folder.FullPath, "txt", 0),
                out string folder,
                out string name
            );

            Assert.Equal(_folder.FullPath, folder);
            Assert.Equal(string.Empty, name);

            CadGetFileDialog.StartOf(new CadGetFileRequest("T", _folder.FullPath, "txt", 16), out folder, out name);

            Assert.Equal(_folder.FullPath, folder);
            Assert.Equal(string.Empty, name);
        }

        [Fact]
        public void TheStart_OfAFolderThatIsMissing_OrNoName_LeavesTheDialogsOwn()
        {
            CadGetFileDialog.StartOf(
                new CadGetFileRequest("T", Path.Combine(_folder.FullPath, "none", "a.txt"), "txt", 0),
                out string folder,
                out string name
            );

            Assert.Equal(string.Empty, folder);
            Assert.Equal("a.txt", name);

            CadGetFileDialog.StartOf(new CadGetFileRequest("T", string.Empty, "txt", 0), out folder, out name);

            Assert.Equal(string.Empty, folder);
            Assert.Equal(string.Empty, name);
        }

        #endregion

        #region read-line

        [Fact]
        public void ALineTypedForReadLine_TakesSpaces_AndIsTheValueWithItsSpaces()
        {
            // AutoCAD 2021 (r7): (read-line) then "hello world" echoes the line alone and gives "hello world"
            MainViewModel model = Opened();

            model.Execute("(read-line)");
            Assert.True(model.Session.IsLispWaiting);
            model.CommandText = "hel";
            Assert.True(model.PromptTakesSpaces);
            model.CommandText = string.Empty;
            model.Execute("hello world");

            Assert.Equal("\"hello world\"", model.History.Last());
            Assert.Equal("hello world", model.History[model.History.Count - 2]);
            Assert.False(model.Session.IsLispWaiting);
        }

        #endregion

        #region Files left open

        [Fact]
        public void AFileARoutineLeftOpen_IsClosedWhenItsDrawingIsClosed()
        {
            MainViewModel model = Opened();
            string path = Path.Combine(_folder.FullPath, "left-open.txt");
            model.Execute("(setq f (open \"" + Slashed(path) + "\" \"w\"))");
            model.Execute("(write-line \"kept\" f)");

            // the name is locked for deleting while the routine holds the file
            Assert.ThrowsAny<IOException>(() => File.Delete(path));

            Assert.True(model.CloseDocument());

            File.Delete(path);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void AFileARoutineLeftOpen_IsClosedWhenItsDrawingIsReadAgain()
        {
            MainViewModel model = Opened();
            string drawing = model.Document.FileName;
            string path = Path.Combine(_folder.FullPath, "left-open-reload.txt");
            model.Execute("(setq f (open \"" + Slashed(path) + "\" \"w\"))");
            Assert.ThrowsAny<IOException>(() => File.Delete(path));

            Assert.True(model.Reload(drawing, discardChanges: true));

            File.Delete(path);
            Assert.False(File.Exists(path));
        }

        #endregion
    }
}
