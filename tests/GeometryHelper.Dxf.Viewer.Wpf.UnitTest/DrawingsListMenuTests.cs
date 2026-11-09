using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The DRAWINGS list as a list of files (C5): a click selects, a double click or Enter opens, the right-click menu opens
    /// and closes through the window's close path, Delete does nothing.
    /// </summary>
    public sealed class DrawingsListMenuTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void AClickSelectsTheRowAndOpensNothing()
        {
            MainViewModel model = ThreeFiles(out _, out string b, out _);
            Assert.True(model.Open(b));
            FileRowViewModel a = Row(model, "a.dxf");

            model.SelectFileRow(a);

            Assert.True(Row(model, "a.dxf").IsSelected);
            Assert.False(Row(model, "a.dxf").IsOpen);
            Assert.Equal("b.dxf", model.Document.Title);
            Assert.Single(model.SelectedFileRows);
        }

        [Fact]
        public void ADoubleClickOrEnterOpensTheRow_AndShowsAnOpenOneAgain()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(b));

            // A double click is Activate, as the list calls it; Enter opens the row clicked last.
            model.Activate(Row(model, "a.dxf"));
            Assert.Equal("a.dxf", model.Document.Title);
            model.SelectFileRow(Row(model, "b.dxf"));

            Assert.True(model.FileListKey("Enter"));

            Assert.Equal("b.dxf", model.Document.Title);
            Assert.True(Row(model, "a.dxf").IsOpen);
            Assert.True(Row(model, "b.dxf").IsSelected);
        }

        [Fact]
        public void CtrlClickAddsOrTakesOutARow_AndShiftClickTakesTheRowsFromTheLastClicked()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));

            model.SelectFileRow(Row(model, "a.dxf"));
            model.SelectFileRow(Row(model, "c.dxf"), shift: true);
            Assert.Equal(new[] { "a.dxf", "b.dxf", "c.dxf" }, model.SelectedFileRows.Select(r => r.Label));

            model.SelectFileRow(Row(model, "b.dxf"), control: true);
            Assert.Equal(new[] { "a.dxf", "c.dxf" }, model.SelectedFileRows.Select(r => r.Label));

            model.SelectFileRow(Row(model, "b.dxf"));
            Assert.Equal(new[] { "b.dxf" }, model.SelectedFileRows.Select(r => r.Label));
        }

        [Fact]
        public void TheMenuOffersOpenForADrawingNotShown_AndEachCloseOnlyWhereItClosesOne()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));
            model.SelectFileRow(Row(model, "c.dxf"));

            Dictionary<string, bool> onShown = Menu(model, Row(model, "a.dxf"));
            Dictionary<string, bool> onClosed = Menu(model, Row(model, "c.dxf"));

            Assert.Equal(new[] { "Open", "Close", "Close Selected Drawings", "Close All Other Drawings", "Close All Drawings" },
                model.FileRowMenu(Row(model, "a.dxf")).Where(i => !i.IsSeparator).Select(i => i.Header));
            Assert.False(onShown["Open"]);
            Assert.True(onShown["Close"]);
            Assert.False(onShown["Close Selected Drawings"]);
            Assert.False(onShown["Close All Other Drawings"]);
            Assert.True(onShown["Close All Drawings"]);
            Assert.True(onClosed["Open"]);
            Assert.False(onClosed["Close"]);
            Assert.True(onClosed["Close All Other Drawings"]);
        }

        [Fact]
        public void CloseAllOtherDrawingsAsksOnceForThoseWithChanges_KeepsTheRowClickedAndListsTheFilesClosed()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out string c);
            var closed = new List<string>();
            model.AddExtension(new ClosedWatcher(closed));
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            AddLine(model);
            Assert.True(model.Open(c));
            var asked = new List<int>();
            model.AskSaveChanges = d => throw new InvalidOperationException("Several drawings are asked about once.");
            model.AskSaveAll = list =>
            {
                asked.Add(list.Count);
                return CadSaveChoice.DontSave;
            };

            Run(model, Row(model, "c.dxf"), "Close All Other Drawings");

            Assert.Equal(new[] { 2 }, asked);
            Assert.Equal(new[] { "a.dxf", "b.dxf" }, closed);
            Assert.Equal("c.dxf", model.Document.Title);
            Assert.False(Row(model, "a.dxf").IsOpen);
            Assert.False(Row(model, "b.dxf").IsOpen);
            Assert.Equal(3, model.FileRows.Count(r => !r.IsHeader));
            Assert.Equal(3, CadDocument.Open(a).EntityCount);
        }

        [Fact]
        public void CancelOfTheQuestionClosesNone_AndCloseSelectedAsksAboutTheOneDrawingWithChanges()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out string c);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            AddLine(model);
            Assert.True(model.Open(c));
            model.AskSaveAll = list => CadSaveChoice.Cancel;

            Run(model, Row(model, "a.dxf"), "Close All Drawings");

            Assert.Equal(3, model.FileRows.Count(r => r.IsOpen));
            int asked = 0;
            model.AskSaveChanges = d =>
            {
                asked++;
                Assert.Equal("a.dxf", d.Title);
                return CadSaveChoice.DontSave;
            };
            model.SelectFileRow(Row(model, "a.dxf"));
            model.SelectFileRow(Row(model, "c.dxf"), control: true);

            Run(model, Row(model, "a.dxf"), "Close Selected Drawings");

            Assert.Equal(1, asked);
            Assert.Equal(new[] { "b.dxf" }, model.FileRows.Where(r => r.IsOpen).Select(r => r.Label));
            Assert.Equal("b.dxf", model.Document.Title);
        }

        [Fact]
        public void CloseOfTheMenuAsksAboutChanges_AndNoAnswerKeepsTheDrawingOpen()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));
            AddLine(model);

            Run(model, Row(model, "a.dxf"), "Close");

            Assert.True(model.HasDocument);
            Assert.True(Row(model, "a.dxf").IsOpen);
        }

        [Fact]
        public void DeleteInTheListIsTakenAndChangesNothing()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));
            model.ClickAt(new GeoPoint2(2000, 0), 1.0);
            Assert.Equal(1, model.Document.Selection.Count);
            model.SelectFileRow(Row(model, "a.dxf"));

            Assert.True(model.FileListKey("Delete"));

            Assert.Equal(3, model.Document.EntityCount);
            Assert.Equal(1, model.Document.Selection.Count);
            Assert.Equal(3, model.FileRows.Count(r => !r.IsHeader));
            Assert.True(Row(model, "a.dxf").IsSelected);
            Assert.False(model.FileListKey("Escape"));
        }

        private MainViewModel ThreeFiles(out string a, out string b, out string c)
        {
            a = _folder.Write("a.dxf", TestFiles.Room(out _, out _));
            b = _folder.Write("b.dxf", TestFiles.Room(out _, out _));
            c = _folder.Write("c.dxf", TestFiles.Room(out _, out _));
            return new MainViewModel();
        }

        private static FileRowViewModel Row(MainViewModel model, string label) => model.FileRows.Single(r => r.Label == label);

        private static Dictionary<string, bool> Menu(MainViewModel model, FileRowViewModel row)
            => model.FileRowMenu(row).Where(i => !i.IsSeparator).ToDictionary(i => i.Header, i => i.IsEnabled);

        private static void Run(MainViewModel model, FileRowViewModel row, string header)
        {
            CadMenuItemViewModel item = model.FileRowMenu(row).Single(i => i.Header == header);
            Assert.True(item.Command.CanExecute(null));
            item.Command.Execute(null);
        }

        private static void AddLine(MainViewModel model)
        {
            CadDocument document = model.Document;
            Assert.True(document.ApplyEdit("LINE", null, scope => scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0)))).Succeeded);
        }

        /// <summary>An extension that writes down each document it is told is closed.</summary>
        private sealed class ClosedWatcher : CadWorkspaceExtension
        {
            private readonly List<string> _closed;

            public ClosedWatcher(List<string> closed)
            {
                _closed = closed;
            }

            public override void DocumentClosed(CadDocument document) => _closed.Add(document.Title);
        }
    }
}
