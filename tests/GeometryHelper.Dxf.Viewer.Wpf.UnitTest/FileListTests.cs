using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A document shown under a key of its own and saved to a file is one row of the list, not two.</summary>
    public class FileListTests
    {
        [Fact]
        public void AFileADocumentShownUnderAKeyOfItsOwnIsSavedToIsNotListedAgainWhenItsFolderIsListed()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                CadDocument made = CadDocument.From(TestFiles.Room(out _, out _), "B-101");
                model.OpenDocument(made, "drawing:B1");
                string saved = Path.Combine(folder.FullPath, "B-101.dxf");
                made.SaveAs(saved);
                string other = folder.Write("other.dxf", TestFiles.Room(out _, out _));

                Assert.True(model.Open(other));

                Assert.DoesNotContain(model.FileRows, r => string.Equals(r.Path, saved, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(2, model.FileRows.Count(r => !r.IsHeader));
                Assert.Equal("All 2", model.AllChipText);

                // Closed, the file is listed as a file of its folder again.
                Assert.True(model.CloseDocument(made));
                Assert.Contains(model.FileRows, r => string.Equals(r.Path, saved, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void ADocumentSavedUnderItsFileAndShownAgainUnderItsKeyIsListedOnce()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                CadDocument made = CadDocument.From(TestFiles.Room(out _, out _), "B-101");
                model.OpenDocument(made, "drawing:B1");
                Assert.True(model.SaveAs(Path.Combine(folder.FullPath, "B-101.dxf")));
                model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "B-102"), "drawing:B2");

                model.OpenDocument(made, "drawing:B1");

                Assert.Same(made, model.Document);
                Assert.Equal(2, model.FileRows.Count(r => !r.IsHeader));
                Assert.Single(model.FileRows, r => r.IsCurrent);
            }
        }
    }
}
