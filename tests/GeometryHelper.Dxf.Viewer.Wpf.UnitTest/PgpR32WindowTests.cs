using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// OPEN typed, DRAWINGRECOVERY and the quick views QVDRAWING and QVLAYOUT with their CLOSE of AutoCAD 2021's acad.pgp, the
    /// window's own commands of round 3: what each asks in AutoCAD's words and what it shows, with no drawing open too.
    /// </summary>
    public class PgpR32WindowTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "r32-" + Guid.NewGuid().ToString("N"));

        public PgpR32WindowTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private string Drawing(string name)
        {
            string path = Path.Combine(_folder, name);
            new DxfDatabase("AC1027").Save(path);
            return path;
        }

        /// <summary>A window with a.dxf of the temporary folder open, and FILEDIA 0 where asked.</summary>
        private MainViewModel Shown(bool typed)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Drawing("a.dxf")));

            if (typed)
            {
                model.Execute("FILEDIA");
                model.Execute("0");
            }

            return model;
        }

        private static string Full(string path) => Path.GetFullPath(path);

        [Fact]
        public void TheAliases_ReachTheCommands()
        {
            MainViewModel model = Shown(false);

            foreach (
                string[] pair in new[]
                {
                    new[] { "OPENURL", "OPEN" },
                    new[] { "DXFIN", "OPEN" },
                    new[] { "DRM", "DRAWINGRECOVERY" },
                    new[] { "QVD", "QVDRAWING" },
                    new[] { "QVDC", "QVDRAWINGCLOSE" },
                    new[] { "QVL", "QVLAYOUT" },
                    new[] { "QVLC", "QVLAYOUTCLOSE" },
                }
            )
            {
                Assert.Equal(pair[1], CadShortcuts.Default.Aliases[pair[0]]);
                Assert.Equal(pair[1], model.Session.Registry.Find(pair[0])?.Name);
            }
        }

        #region OPEN

        [Fact]
        public void Open_AsksTheWindowForItsDialog_OnceTheCommandHasEnded_WithOrWithoutADrawing()
        {
            MainViewModel model = Shown(false);
            int asked = 0;
            model.OpenRequested += (s, e) => asked++;

            model.Execute("OPEN");
            Assert.Equal(1, asked);
            model.Execute("DXFIN");
            Assert.Equal(2, asked);

            var empty = new MainViewModel();
            empty.OpenRequested += (s, e) => asked++;
            empty.Execute("OPEN");
            Assert.Equal(3, asked);
            empty.Execute("OPENURL");
            Assert.Equal(4, asked);
        }

        [Fact]
        public void Open_WithNoWindowToShowTheDialog_SaysSo()
        {
            var model = new MainViewModel();
            model.Execute("OPEN");

            Assert.Contains("OPEN is shown by the window: Ctrl+O.", model.History);
        }

        [Fact]
        public void Open_WithFiledia0_AsksTheNameOnTheCommandLine_AndOpensTheDrawing()
        {
            MainViewModel model = Shown(true);
            string other = Drawing("other.dxf");
            int asked = 0;
            model.OpenRequested += (s, e) => asked++;

            model.Execute("OPEN");
            Assert.Equal("Enter name of drawing to open:", model.Prompt);
            model.Execute(other);

            Assert.Equal(0, asked);
            Assert.Equal(Full(other), Full(model.Document.FileName));
            Assert.Contains(model.History, line => line.StartsWith("Opened other.dxf", StringComparison.Ordinal));
        }

        [Fact]
        public void Open_ANameWithNoExtension_IsLookedForInTheFolderOfTheDrawingShown_AsDwgAndThenDxf()
        {
            MainViewModel model = Shown(true);
            string other = Drawing("sheet.dxf");

            model.Execute("OPEN");
            model.Execute("sheet");

            Assert.Equal(Full(other), Full(model.Document.FileName));
        }

        [Fact]
        public void Open_AFileNotFound_IsToldInAutoCadsWords_AndTheDrawingShownStays()
        {
            MainViewModel model = Shown(true);
            string shown = model.Document.FileName;

            model.Execute("OPEN");
            model.Execute("nothere");

            Assert.Equal("Cannot find the specified drawing file.", model.History.Last());
            Assert.Equal(shown, model.Document.FileName);
            Assert.False(model.IsCommandRunning);

            model.Execute("OPEN");
            model.Execute(string.Empty);
            Assert.False(model.IsCommandRunning);
        }

        #endregion

        #region DRAWINGRECOVERY

        [Fact]
        public void DrawingRecovery_ListsTheCopiesAutosaveKept_AndOpensTheOneChosen()
        {
            MainViewModel model = Shown(false);
            string a = Path.Combine(_folder, "a.dxf");
            File.Copy(a, Path.Combine(_folder, "a.autosave1.dxf"));
            File.Copy(a, Path.Combine(_folder, "a.autosave2.dxf"));
            File.Copy(a, Path.Combine(_folder, "a.dwg.autosave1.dxf"));

            model.Execute("DRM");

            Assert.Contains("Backup files:", model.History);
            string[] listed = model.History.Where(l => l.Contains(" KB  saved ")).ToArray();
            Assert.Equal(3, listed.Length);
            Assert.StartsWith("  1  a.autosave1.dxf  ", listed[0]);
            Assert.StartsWith("  2  a.autosave2.dxf  ", listed[1]);
            Assert.StartsWith("  3  a.dwg.autosave1.dxf  ", listed[2]);
            Assert.Equal("Enter number of the backup file to open <none>:", model.Prompt);

            model.Execute("9");
            Assert.Equal("Requires a number from 1 to 3.", model.History.Last());
            Assert.Equal("Enter number of the backup file to open <none>:", model.Prompt);

            model.Execute("2");
            Assert.False(model.IsCommandRunning);
            Assert.Equal(Full(Path.Combine(_folder, "a.autosave2.dxf")), Full(model.Document.FileName));
        }

        [Fact]
        public void DrawingRecovery_WithNoCopy_SaysSo_AndEnterLeavesTheList()
        {
            MainViewModel model = Shown(false);
            model.Execute("DRAWINGRECOVERY");
            Assert.Equal("No backup files were found beside the drawings listed.", model.History.Last());
            Assert.False(model.IsCommandRunning);

            File.Copy(Path.Combine(_folder, "a.dxf"), Path.Combine(_folder, "a.autosave1.dxf"));
            string shown = model.Document.FileName;
            model.Execute("DRAWINGRECOVERY");
            model.Execute(string.Empty);
            Assert.False(model.IsCommandRunning);
            Assert.Equal(shown, model.Document.FileName);
        }

        [Fact]
        public void DrawingRecovery_WithNoDrawingOpen_ListsTheCopies_AndLeavesOpeningOneToOpen()
        {
            var model = new MainViewModel();
            model.Execute("DRM");
            Assert.Equal("No backup files were found beside the drawings listed.", model.History.Last());
        }

        #endregion

        #region The quick views

        [Fact]
        public void TheQuickViewsOfDrawings_ShowAndHideTheDrawingsList_AndThoseOfLayouts_TheTabs()
        {
            MainViewModel model = Shown(false);
            Assert.True(model.IsFileListShown);
            Assert.True(model.AreSpaceTabsShown);

            model.Execute("QVDRAWINGCLOSE");
            Assert.False(model.IsFileListShown);
            model.Execute("QVD");
            Assert.True(model.IsFileListShown);
            model.Execute("QVDC");
            Assert.False(model.IsFileListShown);
            model.Execute("QVDRAWING");
            Assert.True(model.IsFileListShown);

            model.Execute("QVLAYOUTCLOSE");
            Assert.False(model.AreSpaceTabsShown);
            model.Execute("QVL");
            Assert.True(model.AreSpaceTabsShown);
            model.Execute("QVLC");
            Assert.False(model.AreSpaceTabsShown);
            model.Execute("QVLAYOUT");
            Assert.True(model.AreSpaceTabsShown);
        }

        [Fact]
        public void TheQuickViews_NeedNoDrawing()
        {
            var model = new MainViewModel();

            model.Execute("QVDC");
            Assert.False(model.IsFileListShown);
            model.Execute("QVD");
            Assert.True(model.IsFileListShown);
            model.Execute("QVLC");
            Assert.False(model.AreSpaceTabsShown);
            model.Execute("QVL");
            Assert.True(model.AreSpaceTabsShown);
        }

        #endregion
    }
}
