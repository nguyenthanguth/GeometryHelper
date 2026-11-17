using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>SAVEALL and CLOSEALL in the window's view model, and the Inspection Dimension dialog of DIMINSPECT.</summary>
    public sealed class SaveAllAndDimInspectTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

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

        [Fact]
        public void SaveAllSavesEveryDrawingWithChanges_AndTheOneShownStaysShown()
        {
            var model = new MainViewModel();
            string first = _folder.Write("first.dxf", TestFiles.Room(out _, out _));
            string second = _folder.Write("second.dxf", TestFiles.Room(out _, out _));
            string third = _folder.Write("third.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(first));
            AddLine(model);
            Assert.True(model.Open(second));
            Assert.True(model.Open(third));
            AddLine(model);
            Assert.True(model.Open(second));
            AddLine(model);

            model.Execute("SAVEALL");

            Assert.Equal(4, CadDocument.Open(first).EntityCount);
            Assert.Equal(4, CadDocument.Open(second).EntityCount);
            Assert.Equal(4, CadDocument.Open(third).EntityCount);
            Assert.Equal("second.dxf", model.Document.Title);
            Assert.Empty(model.UnsavedDocuments);
            Assert.Equal(
                "Saved 3 drawings.",
                model.History.Last(h => h.StartsWith("Saved ", StringComparison.Ordinal))
            );
        }

        [Fact]
        public void SaveAllWithNothingChanged_SaysSo_AndLeavesTheFilesAlone()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            byte[] before = File.ReadAllBytes(room);
            Assert.True(model.Open(room));

            model.Execute("SAVEALL");

            Assert.Equal("No drawing has changes to save.", model.History.Last());
            Assert.Equal(before, File.ReadAllBytes(room));
        }

        [Fact]
        public void SaveAllOfADrawingWithNoFile_AsksSaveAs_AndCancelLeavesItUnsaved()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "new.dxf"));
            AddLine(model);
            int asked = 0;
            model.SaveAsRequested += (s, e) => asked++;

            Assert.False(model.SaveAllDocuments());

            Assert.Equal(1, asked);
            Assert.Single(model.UnsavedDocuments);
        }

        [Fact]
        public void CloseAllAsksOnceForTheDrawingsNotSaved_AndClosesEveryDrawing()
        {
            var model = new MainViewModel();
            string first = _folder.Write("first.dxf", TestFiles.Room(out _, out _));
            string second = _folder.Write("second.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(first));
            AddLine(model);
            Assert.True(model.Open(second));
            AddLine(model);
            int asked = 0;
            model.AskSaveAll = list =>
            {
                asked++;
                Assert.Equal(2, list.Count);
                return CadSaveChoice.Cancel;
            };

            model.Execute("CLOSEALL");
            Assert.Equal(1, asked);
            Assert.True(model.HasDocument);
            Assert.Equal(3, CadDocument.Open(first).EntityCount);

            model.AskSaveAll = list => CadSaveChoice.Save;
            model.Execute("CLOSEALL");

            Assert.False(model.HasDocument);
            Assert.Equal(4, CadDocument.Open(first).EntityCount);
            Assert.Equal(4, CadDocument.Open(second).EntityCount);
        }

        [Fact]
        public void CloseAllWithNoChanges_ClosesWithoutAsking()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("a.dxf", TestFiles.Room(out _, out _))));
            Assert.True(model.Open(_folder.Write("b.dxf", TestFiles.Room(out _, out _))));
            model.AskSaveAll = list => throw new InvalidOperationException("nothing to ask");

            model.Execute("CLOSEALL");

            Assert.False(model.HasDocument);
        }

        [Fact]
        public void DiminspectShowsTheDialogOverTheDimensionsSelected_AndOkWritesItsChoices()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "dim.dxf"));
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("100,0");
            model.Execute("50,20");
            DxfDimension dimension = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single();
            DimInspectViewModel shown = null;
            model.ShowDimInspect = dialog =>
            {
                shown = dialog;
                Assert.Equal("Inspection Dimension", dialog.WindowTitle);
                Assert.Equal("1 dimension selected.", dialog.Selected);
                Assert.True(dialog.IsRound);
                Assert.False(dialog.CanRemove);
                dialog.IsAngular = true;
                Assert.False(dialog.IsRound);
                Assert.True(dialog.IsAngular);
                dialog.Label = "  A1 ";
                dialog.Rate = " ";
                Assert.True(dialog.Accept());
                return true;
            };
            model.Document.Selection.Set(new[] { dimension.Handle });

            model.Execute("DIMINSPECT");

            Assert.NotNull(shown);
            string data = string.Join(
                ";",
                model.Document.FindEntity(dimension.Handle).XData.Skip(1).Select(g => g.TypeCode + "=" + g.Value)
            );
            Assert.Equal("1070=396;1000=100%;1070=395;1000=A1;1070=394;1070=50;1070=393;1070=1", data);
        }

        [Fact]
        public void TheInspectionDialog_OffersRemoveWhereOneHasAnInspection_AndRemoveTakesItOut()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "dim.dxf"));
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("100,0");
            model.Execute("50,20");
            string handle = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single().Handle;
            model.Document.Selection.Set(new[] { handle });
            model.ShowDimInspect = dialog =>
            {
                dialog.IsNone = true;
                dialog.Label = "X";
                return dialog.Accept();
            };
            model.Execute("DIMINSPECT");

            bool offered = false;
            model.ShowDimInspect = dialog =>
            {
                offered = dialog.CanRemove;
                Assert.True(dialog.IsNone);
                Assert.Equal("X", dialog.Label);
                dialog.RemoveInspection();
                return true;
            };
            model.Document.Selection.Set(new[] { handle });
            model.Execute("DIMINSPECT");

            Assert.True(offered);
            Assert.Equal("1 of inspection removed", model.History.Last());
            Assert.DoesNotContain(model.Document.FindEntity(handle).XData, g => g.Value == "ACAD_DSTYLE_DIMINSPECT");
        }

        [Fact]
        public void TheInspectionWindowIsMadeOverItsViewModel_AndCancelWritesNothing()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "dim.dxf"));
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("100,0");
            model.Execute("50,20");
            string handle = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single().Handle;
            model.Document.Selection.Set(new[] { handle });
            model.ShowDimInspect = dialog =>
            {
                dialog.IsNone = true;
                dialog.Cancel();
                return true;
            };

            model.Execute("DIMINSPECT");

            Assert.DoesNotContain(model.Document.FindEntity(handle).XData, g => g.Value == "ACAD_DSTYLE_DIMINSPECT");

            Exception failed = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var request = new CadDimInspectRequest(new[] { (DxfDimension)model.Document.FindEntity(handle) });
                    var window = new DimInspectWindow(new DimInspectViewModel(request));
                    FrameworkElement body = window.Body;
                    body.Measure(new Size(460, 330));
                    body.Arrange(new Rect(0, 0, 460, 330));
                    body.UpdateLayout();
                    Assert.True(body.ActualWidth > 0);
                }
                catch (Exception e)
                {
                    failed = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failed);
        }
    }
}
