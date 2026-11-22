using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The first Text Style dialog, which paid for reading 2 500 font files and 1 400 SHX heads and for a row of the font
    /// list for every font: the SHX list remembers what it read of a file and reads it again only where the file changed,
    /// the font lists are read ahead once the window has drawn, and a long drop-down makes the rows it shows.
    /// </summary>
    public sealed class FontListWarmUpTests : IDisposable
    {
        private static readonly TimeSpan Generous = TimeSpan.FromSeconds(60);
        private readonly TestFolder _folder = new TestFolder();

        /// <summary>Waits for a task, failing the test where it is not done in a minute, and rethrows what it threw.</summary>
        private static async Task Done(Task task)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(Generous));
            Assert.Same(task, finished);
            await task;
        }

        public void Dispose() => _folder.Dispose();

        private static byte[] Head(bool big) =>
            Encoding.ASCII.GetBytes(big ? "AutoCAD-86 bigfont 1.0\r\n\u001a" : "AutoCAD-86 shapes 1.0\r\n\u001a");

        private string Shx(string name, bool big, string folder = null)
        {
            string path = Path.Combine(folder ?? _folder.FullPath, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Head(big));
            return path;
        }

        private string Sub(string name)
        {
            string path = Path.Combine(_folder.FullPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>The list as it was before the cache: every file of the folders opened and its head read, the first folder's file where two share a name.</summary>
        private static List<KeyValuePair<string, bool>> Uncached(params string[] folders)
        {
            var found = new SortedDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in folders.Where(Directory.Exists))
            {
                foreach (
                    string path in Directory.GetFiles(folder, "*.shx").OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                )
                {
                    string head = Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 18);

                    if (!found.ContainsKey(Path.GetFileName(path)))
                    {
                        found[Path.GetFileName(path)] = head.StartsWith("AutoCAD-86 bigfont", StringComparison.Ordinal);
                    }
                }
            }

            return found.ToList();
        }

        [Fact]
        public void TheShxList_FromTheCache_IsTheListOfReadingEveryFile_AtTheFirstAskAndTheSecond()
        {
            Shx("zeta.shx", false);
            Shx("Alpha.shx", false);
            Shx("hztxt.shx", true);
            _folder.WriteText("notes.txt", "not a font");
            string folder = _folder.FullPath;

            List<KeyValuePair<string, bool>> first = TextStyleViewModel.ShxFiles(new[] { folder });
            List<KeyValuePair<string, bool>> second = TextStyleViewModel.ShxFiles(new[] { folder });

            Assert.Equal(new[] { "Alpha.shx", "hztxt.shx", "zeta.shx" }, first.Select(p => p.Key));
            Assert.Equal(Uncached(folder), first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void AFileOfTheFirstFolder_BeatsOneOfTheSameNameInTheSecond_FromTheCacheToo()
        {
            string one = Sub("one");
            string two = Sub("two");
            Shx("same.shx", true, one);
            Shx("same.shx", false, two);
            Shx("only.shx", false, two);

            TextStyleViewModel.ShxFiles(new[] { one, two });
            List<KeyValuePair<string, bool>> cached = TextStyleViewModel.ShxFiles(new[] { one, two });

            Assert.Equal(Uncached(one, two), cached);
            Assert.True(cached.Single(p => p.Key == "same.shx").Value);
        }

        [Fact]
        public void AFileChangedAfterItWasListed_IsReadAgain_AFileAddedOrTakenAwayShowsAtOnce()
        {
            string small = Shx("small.shx", false);
            string gone = Shx("gone.shx", true);
            TextStyleViewModel.ShxFiles(new[] { _folder.FullPath });

            // small.shx becomes a big font (a longer head, a later time written); gone.shx is deleted; new.shx appears.
            File.WriteAllBytes(small, Head(true).Concat(Encoding.ASCII.GetBytes("more")).ToArray());
            File.SetLastWriteTimeUtc(small, DateTime.UtcNow.AddMinutes(5));
            File.Delete(gone);
            Shx("new.shx", true);

            List<KeyValuePair<string, bool>> now = TextStyleViewModel.ShxFiles(new[] { _folder.FullPath });

            Assert.Equal(Uncached(_folder.FullPath), now);
            Assert.Equal(new[] { "new.shx", "small.shx" }, now.Select(p => p.Key));
            Assert.All(now, p => Assert.True(p.Value));
        }

        [Fact]
        public void TheHeadOfAListedFile_IsNotReadAgain_AsAFileOpenInAnotherProgramShows()
        {
            string big = Shx("big.shx", true);
            TextStyleViewModel.ShxFiles(new[] { _folder.FullPath });

            // Opened exclusively, the file cannot be read; the list still says what was read of it before.
            using (new FileStream(big, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.True(TextStyleViewModel.ShxFiles(new[] { _folder.FullPath }).Single().Value);
            }
        }

        [Fact]
        public void AFileThatCouldNotBeRead_IsNotRememberedAsASmallFont()
        {
            string big = Shx("big.shx", true);

            using (new FileStream(big, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.False(TextStyleViewModel.ShxFiles(new[] { _folder.FullPath }).Single().Value);
            }

            Assert.True(TextStyleViewModel.ShxFiles(new[] { _folder.FullPath }).Single().Value);
        }

        [Fact]
        public async Task ReadingTheListsAhead_ReadsTheFoldersOfGlobalSettings_SoTheDialogReadsNoFileOfThem()
        {
            string big = Shx("hztxt.shx", true);
            Shx("romans.shx", false);
            var model = new MainViewModel();
            GlobalSettings settings = model.Settings.Clone();
            settings.FontSearchFolders = new[] { _folder.FullPath };
            model.ApplySettings(settings);
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "w.dxf"));

            await Done(model.WarmUpFonts());

            // The big font is open in another program now: only a list read ahead can still say it is one.
            using (new FileStream(big, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                TextStyleViewModel dialog = model.CreateTextStyle();

                Assert.Equal(new[] { "hztxt.shx" }, dialog.BigFonts);
                Assert.Contains(dialog.FontNames, f => f.Name == "romans.shx");
            }
        }

        [Fact]
        public async Task ReadingTheListsAheadOfFoldersThatAreNotThere_FailsNoTask()
        {
            string missing = Path.Combine(_folder.FullPath, "nowhere");

            await Done(FontListWarmUp.Start(new[] { missing, string.Empty, null }));
            await Done(FontListWarmUp.Start(null));
            Assert.Empty(TextStyleViewModel.ShxFiles(new[] { missing }));
        }

        [Fact]
        public void ALongFontList_MakesTheRowsItShows_NotOneForEveryFont()
        {
            OnSta(() =>
            {
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "w.dxf"));
                var window = new TextStyleWindow(model.CreateTextStyle());
                var box = new ComboBox
                {
                    Style = (Style)window.FindResource("GhComboBox"),
                    ItemsSource = Enumerable.Range(0, 1500).Select(i => "Font " + i).ToList(),
                    SelectedIndex = 0,
                };
                window.Content = box;
                box.ApplyTemplate();
                var popup = (Popup)box.Template.FindName("PART_Popup", box);
                popup.Child.Measure(new Size(400, 600));
                popup.Child.Arrange(new Rect(0, 0, 400, 600));
                ((FrameworkElement)popup.Child).UpdateLayout();

                int rows = Enumerable
                    .Range(0, box.Items.Count)
                    .Count(i => box.ItemContainerGenerator.ContainerFromIndex(i) != null);

                Assert.InRange(rows, 1, 150);
            });
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
