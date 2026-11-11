using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The arrowhead of QLEADER's Leader Settings as AutoCAD 2021 offers it: the arrowheads with their pictures, then
    /// "User Arrow...", which lists the drawing's blocks (AutoCAD's Select Custom Arrow Block) and makes the one chosen the
    /// arrow, written on OK as DIMLDRBLK and group 3 of the settings. Driven through the view model with no window shown.
    /// </summary>
    public sealed class LeaderSettingsArrowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room drawing with a block TICK of one line.</summary>
        private MainViewModel RoomWithBlock()
        {
            var model = new MainViewModel();
            Assert.True(
                model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _)))
            );
            model.Document.ApplyEdit("block", null, s =>
            {
                var block = new DxfBlockTableRecord { Name = "TICK", Origin = new GeoPoint3(0, 0, 0) };
                s.Database.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(-0.5, -0.5, 0), new GeoPoint3(0.5, 0.5, 0)));
            });
            return model;
        }

        [Fact]
        public void UserArrow_IsTheLastEntry_ListsTheDrawingsBlocks_AndOkWritesTheBlockChosen()
        {
            MainViewModel model = RoomWithBlock();
            model.ShowLeaderSettings = dialog =>
            {
                Assert.Equal(LeaderSettingsViewModel.UserArrowEntry, dialog.Arrows.Last());
                Assert.Equal("Closed filled", dialog.Arrows.First());
                Assert.False(dialog.IsChoosingUserArrow);

                // Chosen from the list, it asks for a block of the drawing; a name of none is refused and said.
                dialog.Arrow = LeaderSettingsViewModel.UserArrowEntry;
                Assert.True(dialog.IsChoosingUserArrow);
                Assert.Equal("Closed filled", dialog.Arrow);
                Assert.Contains("TICK", dialog.UserArrowBlocks);
                dialog.UserArrowBlock = "NOPE";
                Assert.False(dialog.KeepUserArrow());
                Assert.True(dialog.IsChoosingUserArrow);
                Assert.Equal("The drawing has no block NOPE.", dialog.Notice);

                // Letters of either case name the block; it becomes the arrow and joins the list before User Arrow...
                dialog.UserArrowBlock = "tick";
                Assert.True(dialog.KeepUserArrow());
                Assert.False(dialog.IsChoosingUserArrow);
                Assert.Equal("TICK", dialog.Arrow);
                Assert.Equal("TICK", dialog.Arrows[dialog.Arrows.Count - 2]);
                Assert.Equal("TICK", dialog.SelectedArrowChoice.Name);
                Assert.True(dialog.Ok());
                return true;
            };

            model.StartCommand("QLEADER");
            model.Execute("S");
            model.Escape();

            Assert.Equal("TICK", model.Document.Database.Dimldrblk?.Name);
            Assert.Equal("TICK", CadQLeaderSettings.Read(model.Document.Database).UserArrow);

            // Opened again, the dialog shows the user arrow; Cancel at the blocks leaves the arrow as it was.
            model.ShowLeaderSettings = dialog =>
            {
                Assert.Equal("TICK", dialog.Arrow);
                dialog.Arrow = "Dot";
                dialog.SelectedArrowChoice = dialog.ArrowChoices.Last();
                Assert.True(dialog.IsChoosingUserArrow);
                dialog.DropUserArrow();
                Assert.False(dialog.IsChoosingUserArrow);
                Assert.Equal("Dot", dialog.Arrow);
                dialog.Cancel();
                return true;
            };
            model.StartCommand("QLEADER");
            model.Execute("S");
            model.Escape();
            Assert.Equal("TICK", model.Document.Database.Dimldrblk?.Name);
        }

        [Fact]
        public void UserArrow_InADrawingOfNoBlock_SaysSo_AndAsksNothing()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("blank.dxf", new DxfDatabase("AC1027"))));
            model.ShowLeaderSettings = dialog =>
            {
                dialog.Arrow = LeaderSettingsViewModel.UserArrowEntry;
                Assert.False(dialog.IsChoosingUserArrow);
                Assert.Equal("The drawing has no block to use as an arrow.", dialog.Notice);
                Assert.Equal("Closed filled", dialog.Arrow);
                dialog.Cancel();
                return true;
            };

            model.StartCommand("QLEADER");
            model.Execute("S");
            model.Escape();
        }

        [Fact]
        public void EachArrowhead_HasAPicture_AndTheDialogIsMadeWithTheBlocksAsked()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = RoomWithBlock();
                    LeaderSettingsViewModel shown = null;
                    model.ShowLeaderSettings = d =>
                    {
                        shown = d;
                        return true;
                    };
                    model.StartCommand("QLEADER");
                    model.Execute("S");
                    model.Escape();

                    // Every arrowhead and a user arrow drawn 22 by 12, as the list shows them; User Arrow... has none.
                    shown.Arrow = LeaderSettingsViewModel.UserArrowEntry;
                    shown.UserArrowBlock = "TICK";
                    Assert.True(shown.KeepUserArrow());

                    foreach (LeaderArrowChoiceViewModel choice in shown.ArrowChoices)
                    {
                        if (choice.IsUserArrowEntry)
                        {
                            Assert.Null(choice.Picture);
                            continue;
                        }

                        Assert.NotNull(choice.Picture);
                        Assert.Equal(22, choice.Picture.PixelWidth);
                        Assert.Equal(12, choice.Picture.PixelHeight);
                    }

                    // The window, on the Leader Line & Arrow tab with the blocks asked, is laid out.
                    shown.Tab = LeaderSettingsViewModel.LineTab;
                    shown.Arrow = LeaderSettingsViewModel.UserArrowEntry;
                    var window = new LeaderSettingsWindow(shown);
                    var body = (FrameworkElement)window.Content;
                    window.Content = null;
                    body.DataContext = shown;
                    body.Measure(new Size(window.Width, window.Height));
                    body.Arrange(new Rect(0, 0, window.Width, window.Height));
                    body.UpdateLayout();
                    Assert.True(body.ActualWidth > 0);
                    window.Close();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(failure);
        }
    }
}
