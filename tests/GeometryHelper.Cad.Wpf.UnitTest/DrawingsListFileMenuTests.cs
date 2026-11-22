using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The right-click menu of the DRAWINGS list as AutoCAD 2021's file tab menu has it: New, Open, Save, Save As, Save All;
    /// Close, Close Selected Drawings, Close All, Close All Other Drawings; Copy Full File Path, Open File Location. Save, Save As
    /// and Close act on the row clicked; the empty part of the list offers four of them.
    /// </summary>
    public sealed class DrawingsListFileMenuTests : IDisposable
    {
        private const string Rule = "-";

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ARowOfAnOpenDrawing_HasTheTabMenuInOrder_WithRulesBetweenItsThreeGroups()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));

            Assert.Equal(
                new[]
                {
                    "New...",
                    "Open...",
                    "Save",
                    "Save As...",
                    "Save All",
                    Rule,
                    "Close",
                    "Close All",
                    "Close All Other Drawings",
                    Rule,
                    "Copy Full File Path",
                    "Open File Location",
                },
                Shape(model.FileRowMenu(Row(model, "a.dxf")))
            );
        }

        [Fact]
        public void ARowOfADrawingNotOpen_HasTheSameItems_WithSaveSaveAsAndCloseGrey_AndOpenOn()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out string c);
            Assert.True(model.Open(a));
            Assert.True(model.Open(c));
            model.WriteClipboardText = text => { };

            IReadOnlyList<CadMenuItemViewModel> menu = model.FileRowMenu(Row(model, "b.dxf"));
            Dictionary<string, bool> on = Enabled(menu);

            Assert.Equal(Shape(model.FileRowMenu(Row(model, "a.dxf"))), Shape(menu));
            Assert.False(on["Save"]);
            Assert.False(on["Save As..."]);
            Assert.False(on["Close"]);
            Assert.True(on["New..."]);
            Assert.True(on["Open..."]);
            Assert.True(on["Save All"]);
            Assert.True(on["Close All"]);
            Assert.True(on["Close All Other Drawings"]);
            Assert.True(on["Copy Full File Path"]);
            Assert.True(on["Open File Location"]);
        }

        [Fact]
        public void WithSeveralRowsChosen_CloseSelectedDrawingsStandsBetweenCloseAndCloseAll()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));
            model.SelectFileRow(Row(model, "a.dxf"));
            model.SelectFileRow(Row(model, "b.dxf"), control: true);

            string[] shape = Shape(model.FileRowMenu(Row(model, "a.dxf")));

            Assert.Equal(
                new[] { "Close", "Close Selected Drawings", "Close All", "Close All Other Drawings" },
                shape.Skip(6).Take(4)
            );
            Assert.True(Enabled(model.FileRowMenu(Row(model, "a.dxf")))["Close Selected Drawings"]);
        }

        [Fact]
        public void TheEmptyPartOfTheList_OffersNewOpenSaveAllAndCloseAll()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));

            Assert.Equal(new[] { "New...", "Open...", "Save All", "Close All" }, Shape(model.FileListMenu()));
            Assert.All(model.FileListMenu(), i => Assert.True(i.IsEnabled));
        }

        [Fact]
        public void WithNoDrawingOpen_SaveAllAndCloseAllAreGrey_AndNewAndOpenAreOn()
        {
            MainViewModel model = ThreeFiles(out _, out _, out _);

            Dictionary<string, bool> on = Enabled(model.FileListMenu());

            Assert.True(on["New..."]);
            Assert.True(on["Open..."]);
            Assert.False(on["Save All"]);
            Assert.False(on["Close All"]);
        }

        [Fact]
        public void CloseAllOtherDrawings_IsGreyWithOnlyOneDrawingOpen_AndOnWithTwo()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));

            Assert.False(Enabled(model.FileRowMenu(Row(model, "a.dxf")))["Close All Other Drawings"]);
            Assert.False(Enabled(model.FileRowMenu(Row(model, "b.dxf")))["Close All Other Drawings"]);

            Assert.True(model.Open(b));

            Assert.True(Enabled(model.FileRowMenu(Row(model, "a.dxf")))["Close All Other Drawings"]);
        }

        [Fact]
        public void ADrawingNeverSaved_HasNoPath_SoCopyFullFilePathAndOpenFileLocationAreGrey()
        {
            var model = new MainViewModel();
            model.WriteClipboardText = text => { };
            model.NewDrawing();
            FileRowViewModel row = model.FileRows.Single(r => !r.IsHeader);

            Dictionary<string, bool> on = Enabled(model.FileRowMenu(row));

            Assert.False(on["Copy Full File Path"]);
            Assert.False(on["Open File Location"]);
            Assert.True(on["Save"]);
            Assert.True(on["Close"]);
        }

        [Fact]
        public void CopyFullFilePath_PutsTheFullPathOfTheRowOnTheClipboard()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            var copied = new List<string>();
            model.WriteClipboardText = copied.Add;
            Assert.True(model.Open(a));

            Run(model, Row(model, "a.dxf"), "Copy Full File Path");

            Assert.Equal(new[] { a }, copied);
        }

        [Fact]
        public void CopyFullFilePath_ThroughTheWindowsClipboard_LeavesThePathOnIt()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            model.WriteClipboardText = text => CadWindowsClipboard.TrySetText(text);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));
            string read = null;

            Sta(() =>
            {
                Run(model, Row(model, "b.dxf"), "Copy Full File Path");
                read = CadWindowsClipboard.TryGetText();
            });

            Assert.Equal(b, read);
        }

        [Fact]
        public void OpenFileLocation_StartsExplorerWithTheFileSelected()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out string c);
            var started = new List<ProcessStartInfo>();
            model.StartProcess = started.Add;
            Assert.True(model.Open(a));

            Run(model, Row(model, "c.dxf"), "Open File Location");

            ProcessStartInfo info = Assert.Single(started);
            Assert.Equal("explorer.exe", info.FileName);
            Assert.Equal("/select,\"" + c + "\"", info.Arguments);
        }

        [Fact]
        public void SaveOnARowThatIsNotShown_SavesThatDrawing()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            AddLine(model);
            Assert.Equal("b.dxf", model.Document.Title);
            Assert.Equal(3, CadDocument.Open(a).EntityCount);

            Run(model, Row(model, "a.dxf"), "Save");

            Assert.Equal(4, CadDocument.Open(a).EntityCount);
            Assert.Equal(3, CadDocument.Open(b).EntityCount);
            Assert.False(Row(model, "a.dxf").HasUnsavedChanges);
            Assert.True(Row(model, "b.dxf").HasUnsavedChanges);
        }

        [Fact]
        public void SaveAsOnARowThatIsNotShown_BringsThatDrawingForward_AndAsksTheWindowForItsFile()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));
            var shownWhenAsked = new List<string>();
            model.SaveAsRequested += (s, e) => shownWhenAsked.Add(model.Document.Title);

            Run(model, Row(model, "a.dxf"), "Save As...");

            Assert.Equal(new[] { "a.dxf" }, shownWhenAsked);
        }

        [Fact]
        public void CloseOnARowThatIsNotShown_ClosesThatDrawingOnly_AndKeepsTheOneShown()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));

            Run(model, Row(model, "a.dxf"), "Close");

            Assert.False(Row(model, "a.dxf").IsOpen);
            Assert.True(Row(model, "b.dxf").IsOpen);
            Assert.Equal("b.dxf", model.Document.Title);
        }

        [Fact]
        public void CloseOfADrawingWithChanges_AsksSaveChangesAsBefore()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            var asked = new List<string>();
            model.AskSaveChanges = d =>
            {
                asked.Add(d.Title);
                return CadSaveChoice.DontSave;
            };

            Run(model, Row(model, "a.dxf"), "Close");

            Assert.Equal(new[] { "a.dxf" }, asked);
            Assert.False(Row(model, "a.dxf").IsOpen);
        }

        [Fact]
        public void SaveOnARowThatIsNotShown_ShowsTheDrawingThatWasShownAgain()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            AddLine(model);

            Run(model, Row(model, "a.dxf"), "Save");

            Assert.Equal("b.dxf", model.Document.Title);
            Assert.False(Row(model, "a.dxf").HasUnsavedChanges);
            Assert.True(Row(model, "b.dxf").HasUnsavedChanges);
        }

        [Fact]
        public void SaveOnTheRowShown_StaysOnThatDrawing()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));
            AddLine(model);

            Run(model, Row(model, "b.dxf"), "Save");

            Assert.Equal("b.dxf", model.Document.Title);
            Assert.False(Row(model, "b.dxf").HasUnsavedChanges);
        }

        [Fact]
        public void SaveAsOnARowThatIsNotShown_ShowsTheDrawingThatWasShownAgain_OnceTheWindowHasBeenAsked()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));
            var shownWhenAsked = new List<string>();
            model.SaveAsRequested += (s, e) => shownWhenAsked.Add(model.Document.Title);

            Run(model, Row(model, "a.dxf"), "Save As...");

            Assert.Equal(new[] { "a.dxf" }, shownWhenAsked);
            Assert.Equal("b.dxf", model.Document.Title);
        }

        [Fact]
        public void CloseOnAnotherRowAskingToSaveChanges_SavesThatDrawing_AndKeepsTheOneShown()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out string c);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            Assert.True(model.Open(c));
            model.AskSaveChanges = d => CadSaveChoice.Save;

            Run(model, Row(model, "a.dxf"), "Close");

            Assert.Equal(4, CadDocument.Open(a).EntityCount);
            Assert.False(Row(model, "a.dxf").IsOpen);
            Assert.Equal("c.dxf", model.Document.Title);
        }

        [Fact]
        public void CloseOnAnotherRowWhoseSaveChangesIsCancelled_KeepsBothOpenAndTheOneShown()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            model.AskSaveChanges = d => CadSaveChoice.Cancel;

            Run(model, Row(model, "a.dxf"), "Close");

            Assert.True(Row(model, "a.dxf").IsOpen);
            Assert.Equal("b.dxf", model.Document.Title);
        }

        [Fact]
        public void CloseOnTheRowShown_ShowsAnotherOpenDrawing_AsBefore()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            Assert.True(model.Open(b));

            Run(model, Row(model, "b.dxf"), "Close");

            Assert.False(Row(model, "b.dxf").IsOpen);
            Assert.Equal("a.dxf", model.Document.Title);
        }

        [Fact]
        public void SaveAll_SavesEveryDrawingWithChanges_AndCloseAllClosesThemAll()
        {
            MainViewModel model = ThreeFiles(out string a, out string b, out _);
            Assert.True(model.Open(a));
            AddLine(model);
            Assert.True(model.Open(b));
            AddLine(model);

            Run(model.FileListMenu(), "Save All");

            Assert.Equal(4, CadDocument.Open(a).EntityCount);
            Assert.Equal(4, CadDocument.Open(b).EntityCount);

            Run(model.FileListMenu(), "Close All");

            Assert.False(model.HasDocument);
            Assert.DoesNotContain(model.FileRows, r => r.IsOpen);
        }

        [Fact]
        public void NewOfTheMenu_MakesANewDrawing_AndOpenOfAnOpenRowShowsTheOpenDialog()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));
            int dialogs = 0;
            model.OpenRequested += (s, e) => dialogs++;

            Run(model, Row(model, "a.dxf"), "New...");

            Assert.Equal(2, model.FileRows.Count(r => r.IsOpen));
            Run(model, Row(model, "a.dxf"), "Open...");
            Assert.Equal(1, dialogs);
        }

        [Fact]
        public void OpenOnARowOfADrawingNotOpen_OpensThatFile_AndShowsNoDialog()
        {
            MainViewModel model = ThreeFiles(out string a, out _, out _);
            Assert.True(model.Open(a));
            int dialogs = 0;
            model.OpenRequested += (s, e) => dialogs++;

            Run(model, Row(model, "b.dxf"), "Open...");

            Assert.Equal(0, dialogs);
            Assert.Equal("b.dxf", model.Document.Title);
            Assert.True(Row(model, "b.dxf").IsOpen);
        }

        private MainViewModel ThreeFiles(out string a, out string b, out string c)
        {
            a = _folder.Write("a.dxf", TestFiles.Room(out _, out _));
            b = _folder.Write("b.dxf", TestFiles.Room(out _, out _));
            c = _folder.Write("c.dxf", TestFiles.Room(out _, out _));
            return new MainViewModel();
        }

        private static FileRowViewModel Row(MainViewModel model, string label) =>
            model.FileRows.Single(r => r.Label == label);

        private static string[] Shape(IEnumerable<CadMenuItemViewModel> menu) =>
            menu.Select(i => i.IsSeparator ? Rule : i.Header).ToArray();

        private static Dictionary<string, bool> Enabled(IEnumerable<CadMenuItemViewModel> menu) =>
            menu.Where(i => !i.IsSeparator).ToDictionary(i => i.Header, i => i.IsEnabled);

        private static void Run(MainViewModel model, FileRowViewModel row, string header) =>
            Run(model.FileRowMenu(row), header);

        private static void Run(IEnumerable<CadMenuItemViewModel> menu, string header)
        {
            CadMenuItemViewModel item = menu.Single(i => i.Header == header);
            Assert.True(item.Command.CanExecute(null));
            item.Command.Execute(null);
        }

        private static void AddLine(MainViewModel model)
        {
            CadDocument document = model.Document;
            Assert.True(
                document
                    .ApplyEdit("LINE", null, scope =>
                        scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0)))
                    )
                    .Succeeded
            );
        }

        private static void Sta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(failure.ToString());
            }
        }
    }
}
