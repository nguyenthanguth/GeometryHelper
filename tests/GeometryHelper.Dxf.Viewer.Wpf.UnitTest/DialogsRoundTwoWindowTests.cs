using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialogs of round 2 driven with no window shown: Rename (RENAME), Lineweight Settings (LWEIGHT), Check Spelling
    /// (SPELL) and Boundary Creation (BOUNDARY), each raised by its command through the view model's function.
    /// </summary>
    public class DialogsRoundTwoWindowTests
    {
        /// <summary>Layers WALLS and DOORS, a line on WALLS, a text "Helo wrold" and a square of 100 at (500, 0).</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DOORS" });
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 50, 0),
                    Height = 5,
                    TextString = "Helo wrold",
                }
            );
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(500, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(600, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(600, 100), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(500, 100), 0, 0, 0);
            square.Closed = true;
            db.ModelSpace.AppendEntity(square);
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("round2.dxf", Drawing())));
            return model;
        }

        private static DxfDBText Text(MainViewModel model) =>
            model.Document.ModelSpace.Block.OfType<DxfDBText>().Single();

        [Fact]
        public void Rename_TheDialogRenamesInItsListAtOnce_RefusesATakenOrBadName_AndOkRenamesTheLayer()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowRename = dialog =>
                {
                    Assert.Equal("Blocks", dialog.Kind);
                    dialog.Kind = "Layers";
                    Assert.Equal(new[] { "DOORS", "WALLS" }, dialog.Items);
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("Pick an item to rename.", dialog.Message);
                    dialog.Item = "WALLS";
                    Assert.Equal("WALLS", dialog.OldName);
                    Assert.Equal("WALLS", dialog.NewName);
                    dialog.NewName = "doors";
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("\"DOORS\" already exists.", dialog.Message);
                    dialog.NewName = "A*";
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("Invalid name.", dialog.Message);
                    dialog.NewName = "A-WALL";
                    Assert.True(dialog.RenameTo());
                    Assert.Equal(new[] { "A-WALL", "DOORS" }, dialog.Items);
                    Assert.Equal("A-WALL", dialog.Item);
                    Assert.Equal("", dialog.Message);
                    return true;
                };

                model.Execute("REN");

                Assert.Equal("A-WALL", model.Document.ModelSpace.Block.OfType<DxfLine>().Single().Layer);
                Assert.True(model.Document.Database.LayerTable.Has("A-WALL"));
                Assert.False(model.Document.Database.LayerTable.Has("WALLS"));
            }
        }

        [Fact]
        public void Rename_CancelInTheDialog_RenamesNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowRename = dialog =>
                {
                    dialog.Kind = "Layers";
                    dialog.Item = "WALLS";
                    dialog.NewName = "A-WALL";
                    Assert.True(dialog.RenameTo());
                    return false;
                };

                model.Execute("RENAME");

                Assert.True(model.Document.Database.LayerTable.Has("WALLS"));
                Assert.False(model.Document.Database.LayerTable.Has("A-WALL"));
            }
        }

        [Fact]
        public void Lweight_TheDialogSetsTheLineweightOfNewObjects_TheDisplayAndTheWindowsUnitsDefaultAndScale()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowLineweightSettings = dialog =>
                {
                    Assert.Equal(27, dialog.Items.Count);
                    Assert.Equal(
                        new[] { "ByLayer", "ByBlock", "Default", "0.00 mm" },
                        dialog.Items.Take(4).Select(i => i.Text)
                    );
                    Assert.Equal("Current Lineweight: ByLayer", dialog.CurrentText);
                    Assert.Equal("0.25 mm", dialog.DefaultText);
                    Assert.Equal(5.0, dialog.ScalePosition, 6);
                    dialog.Selected = dialog.Items.Single(i => i.Text == "0.35 mm");
                    dialog.IsInches = true;
                    Assert.Equal("0.010\"", dialog.Items[10].Text);
                    Assert.Equal("Current Lineweight: 0.014\"", dialog.CurrentText);
                    dialog.DefaultText = "0.020\"";
                    dialog.ScalePosition = 10;
                    Assert.Equal(2.0, dialog.Scale, 6);
                    dialog.DisplayLineweight = false;
                    return true;
                };

                model.Execute("LWEIGHT");

                Assert.Equal((DxfLineWeight)35, model.Document.Database.Celweight);
                Assert.Equal("LWEIGHT", model.Document.NextUndo.Name);
                Assert.False(model.ShowLineWeights);
                Assert.True(model.Settings.LineweightsInInches);
                Assert.Equal(50, model.Settings.DefaultLineweight);
                Assert.Equal(2.0, model.Settings.LineweightDisplayScale, 6);
                Assert.Equal(0.5, model.Document.DefaultLineWeight, 6);
            }
        }

        [Fact]
        public void Lweight_CancelInTheDialog_ChangesNothing_AndWithNoDialogTheCommandLineAsks()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.Execute("LW");
                Assert.StartsWith("Enter default lineweight for new objects", model.Prompt);
                model.Escape();

                model.ShowLineweightSettings = dialog =>
                {
                    dialog.Selected = dialog.Items.Last();
                    return false;
                };
                model.Execute("LWEIGHT");

                Assert.Equal(DxfLineWeight.ByLayer, model.Document.Database.Celweight);
                Assert.Null(model.Document.NextUndo);
            }
        }

        [Fact]
        public void Spell_CheckSpellingStartsChangesAndCloses_TheTextWrittenOnce()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowCheckSpelling = dialog =>
                {
                    Assert.False(dialog.IsChecking);
                    Assert.False(dialog.CanSelectObjects);
                    dialog.Start();
                    Assert.Equal("Helo", dialog.Word);
                    Assert.Equal("Helo wrold", dialog.Context);
                    Assert.Equal("Hello", dialog.Replacement);
                    dialog.Change();
                    Assert.Equal("wrold", dialog.Word);
                    dialog.Suggestion = "world";
                    Assert.Equal("world", dialog.Replacement);
                    dialog.Change();
                    Assert.False(dialog.IsChecking);
                    Assert.Equal("Spelling check complete.", dialog.Status);
                    Assert.Equal("Helo wrold", Text(model).TextString);
                };

                model.Execute("SP");

                Assert.Equal("Hello world", Text(model).TextString);
                Assert.Equal("SPELL", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void Spell_SelectObjects_ClosesTheDialogForThePick_AndShowsItAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                int shown = 0;
                model.ShowCheckSpelling = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        dialog.Where = "Selected objects";
                        Assert.True(dialog.CanSelectObjects);
                        dialog.SelectObjects();
                    }
                };

                model.Execute("SPELL");

                Assert.Equal(1, shown);
                Assert.Equal("Select objects:", model.Prompt);
                model.Escape();
            }
        }

        [Fact]
        public void Boundary_PickPointsInTheDialog_AsksThePoints_AndCancelEndsTheCommand()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowBoundaryCreation = dialog =>
                {
                    Assert.True(dialog.IslandDetection);
                    Assert.Equal("Polyline", dialog.ObjectType);
                    Assert.Equal(new[] { "Polyline", "Region" }, dialog.ObjectTypes);
                    return true;
                };

                model.Execute("BO");
                Assert.Equal("Pick internal point:", model.Prompt);
                model.Escape();

                model.ShowBoundaryCreation = dialog => false;
                model.Execute("BOUNDARY");
                Assert.False(model.Session.IsRunning);
            }
        }
    }
}
