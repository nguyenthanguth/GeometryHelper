using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// <see cref="MainViewModel.ShowFolder"/>: the files of a folder listed with none opened, a file deleted since taken off
    /// the list, a file open kept, and the files of another folder left alone.
    /// </summary>
    public sealed class ShowFolderTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheFilesOfAFolder_AreListedUnderOneHeader_AndNoneIsOpened()
        {
            string a = _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            string b = _folder.Write("b.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();

            model.ShowFolder(_folder.FullPath);

            Assert.False(model.HasDocument);
            Assert.Single(model.FileRows, r => r.IsHeader);
            Assert.Equal(
                new[] { a, b }.Select(Path.GetFullPath).ToArray(),
                model.FileRows.Where(r => !r.IsHeader).Select(r => r.Path).ToArray()
            );
            Assert.All(model.FileRows.Where(r => !r.IsHeader), r => Assert.False(r.IsOpen));

            // A trailing separator names the same folder: nothing is listed twice.
            model.ShowFolder(_folder.FullPath + Path.DirectorySeparatorChar);

            Assert.Single(model.FileRows, r => r.IsHeader);
            Assert.Equal(2, model.FileRows.Count(r => !r.IsHeader));
        }

        [Fact]
        public void AFileDeletedAndAFileMadeSince_AreTakenOffAndPutOnTheList()
        {
            string a = _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            _folder.Write("b.dwg", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            model.ShowFolder(_folder.FullPath);

            File.Delete(a);
            string c = _folder.Write("c.dwg", TestFiles.Room(out _, out _));
            model.ShowFolder(_folder.FullPath);

            Assert.Equal(
                new[] { "b.dwg", "c.dwg" },
                model.FileRows.Where(r => !r.IsHeader).Select(r => Path.GetFileName(r.Path)).ToArray()
            );
            Assert.Contains(model.FileRows, r => string.Equals(r.Path, c, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AFileOpenStaysOnTheList_AndTheFilesOfAnotherFolderAreLeftAlone()
        {
            string a = _folder.Write("a.dwg", TestFiles.Room(out _, out _));
            using (var other = new TestFolder())
            {
                string elsewhere = other.Write("z.dwg", TestFiles.Room(out _, out _));
                var model = new MainViewModel();
                Assert.True(model.Open(elsewhere));
                model.ShowFolder(_folder.FullPath);

                Assert.Equal(2, model.FileRows.Count(r => r.IsHeader));
                Assert.True(model.FileRows.Single(r => r.Path == Path.GetFullPath(elsewhere)).IsOpen);

                // Opened and then deleted under it, a file is not taken off while it is open.
                Assert.True(model.Open(a));
                File.Delete(a);
                model.ShowFolder(_folder.FullPath);

                Assert.True(model.FileRows.Single(r => r.Path == Path.GetFullPath(a)).IsOpen);
                Assert.Contains(model.FileRows, r => r.Path == Path.GetFullPath(elsewhere));
            }
        }

        [Fact]
        public void AFolderThatIsNotThere_ListsNothing_AndANullFolderIsRefused()
        {
            var model = new MainViewModel();

            model.ShowFolder(Path.Combine(_folder.FullPath, "missing"));

            Assert.Empty(model.FileRows);
            Assert.Throws<ArgumentNullException>(() => model.ShowFolder(null));
            Assert.Throws<ArgumentNullException>(() => model.ShowFolder(" "));
        }
    }
}
