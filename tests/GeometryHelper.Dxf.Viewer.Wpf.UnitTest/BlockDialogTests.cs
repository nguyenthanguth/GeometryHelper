using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialogs of the block commands (C13), driven with no window: Block Definition, the Blocks palette, Write Block,
    /// Attribute Definition and the Enhanced Attribute Editor, each over the request its command hands the window.
    /// </summary>
    public class BlockDialogTests
    {
        /// <summary>A metric drawing with a circle of radius 10 about (100, 100), a line below it and the attribute GRID at its centre.</summary>
        private static string Bubble()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(100, 100, 0), new GeoVector3(0, 0, 1), 10.0));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 90, 0), new GeoPoint3(100, 70, 0)));
            database.ModelSpace.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(100, 100, 0), "A", "GRID", "Grid letter", null));
            return database.ToText();
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.WriteText("plan-level2.dxf", Bubble())));
            return model;
        }

        /// <summary>Makes the block GB of the bubble through the dialog, the objects selected first, its base point typed.</summary>
        private static void MakeBubble(MainViewModel model)
        {
            model.ShowBlockDefinition = dialog =>
            {
                Assert.Equal("3 objects selected", dialog.ObjectsText);
                Assert.True(dialog.IsConvert);
                dialog.Name = "GB";
                dialog.BaseX = "100";
                dialog.BaseY = "70";
                Assert.True(dialog.CanOk);
                dialog.Close(CadDialogAction.Ok);
                return true;
            };
            model.Execute("SELECTALL");
            model.Execute("B");
        }

        [Fact]
        public void BlockShowsTheDialogWithTheObjectsSelected_AndOkMakesTheBlockAndItsInsert_AsOneStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);

                MakeBubble(model);

                DxfBlockTableRecord block = model.Document.Database.BlockTable["GB"];
                Assert.Equal(new GeoPoint3(100, 70, 0), block.Origin);
                Assert.Equal(3, block.Count);
                DxfBlockReference insert = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
                Assert.Equal("A", Assert.Single(insert.AttributeCollection).TextString);
                Assert.Equal("BLOCK", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void TheNameOfABlockOfTheDrawing_AsksToRedefineIt_AndOneAutoCadRefusesKeepsOkOff()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                var dialog = new BlockDefinitionViewModel(new CadBlockDefinitionRequest(model.Document));

                dialog.Name = "gb";
                Assert.True(dialog.Exists);
                Assert.NotNull(dialog.Preview);

                dialog.Name = "GRID*";
                Assert.False(dialog.CanOk);
                Assert.Equal("No objects selected", dialog.ObjectsText);
                Assert.False(dialog.HasObjects);
            }
        }

        [Fact]
        public void ThePaletteListsTheBlocksDrawn_FiltersThem_AndATileClickedPlacesItsBlock_ThenListedAsRecent()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                model.Execute("-B");
                model.Execute("NOTHING");
                model.Execute("0,0");
                model.Execute(string.Empty);

                BlocksPaletteViewModel shown = null;
                model.ShowBlocksPalette = palette =>
                {
                    shown = palette;
                    Assert.Equal(new[] { "GB", "NOTHING" }, palette.Tiles.Select(t => t.Name).ToArray());
                    Assert.Equal("plan-level2.dxf · 2 blocks", palette.SourceText);
                    palette.Filter = "gb";
                    Assert.Equal("GB", Assert.Single(palette.Tiles).Name);
                    Assert.NotNull(palette.Tiles[0].Picture);
                    palette.Scale = "2";
                    palette.Rotation = "90";
                    Assert.True(palette.Choose(palette.Tiles[0]));
                    return true;
                };

                model.Execute("I");
                model.Execute("500,500");
                model.Execute("C");

                DxfBlockReference placed = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Last();
                Assert.Equal(new GeoVector3(2, 2, 2), placed.ScaleFactors);
                Assert.Equal(Math.PI / 2.0, placed.Rotation, 12);
                Assert.Equal("C", placed.AttributeCollection[0].TextString);
                Assert.Equal(new[] { "GB" }, model.RecentBlocks.ToArray());

                var again = new BlocksPaletteViewModel(new CadInsertRequest(model.Document), model.RecentBlocks) { Tab = "Recent" };
                Assert.Equal("GB", Assert.Single(again.Tiles).Name);
            }
        }

        [Fact]
        public void ThePaletteOpensAnotherDrawing_ItsBlocksAndTheWholeFileListed()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                string other = Path.Combine(folder.FullPath, "other.dxf");
                model.Document.Database.Save(other);
                var palette = new BlocksPaletteViewModel(new CadInsertRequest(model.Document), null);

                Assert.False(palette.OpenOther(Path.Combine(folder.FullPath, "missing.dxf")));
                Assert.True(palette.OpenOther(other));

                Assert.True(palette.IsOtherDrawing);
                Assert.Equal(new[] { "(whole drawing)", "GB" }, palette.Tiles.Select(t => t.DisplayName).ToArray());
                Assert.True(palette.Choose(palette.Tiles[1]));
                Assert.Equal(other, palette.Request.FileName);
                Assert.Equal("GB", palette.Request.Name);
            }
        }

        [Fact]
        public void WriteBlockTakesItsSource_AndOkIsOffUntilObjectsAreGiven()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                var dialog = new WriteBlockViewModel(new CadWriteBlockRequest(model.Document) { FileName = Path.Combine(folder.FullPath, "gb.dxf") });

                Assert.True(dialog.IsObjects);
                Assert.True(dialog.IsRetain);
                Assert.False(dialog.CanOk);
                Assert.Equal("Millimetres", dialog.Unit);

                dialog.IsBlock = true;
                Assert.Equal("GB", dialog.BlockName);
                Assert.True(dialog.CanOk);

                dialog.BaseX = "5";
                dialog.Close(CadDialogAction.Ok);
                Assert.Equal(CadWriteBlockSource.Block, dialog.Request.Source);
                Assert.Equal(new GeoPoint3(5, 0, 0), dialog.Request.BasePoint);
                Assert.Equal(DxfUnits.Millimeters, dialog.Request.Units);
            }
        }

        [Fact]
        public void AttributeDefinitionTakesAWordForItsTag_AndItsAnglesInDegrees()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var dialog = new AttributeDefinitionViewModel(new CadAttributeDefinitionRequest(model.Document, null, 2.5));

                dialog.Tag = "GRID LETTER";
                Assert.False(dialog.CanOk);
                dialog.Tag = "grid";
                dialog.Prompt = "Grid letter";
                dialog.Default = "A";
                Assert.True(dialog.CanOk);
                Assert.Contains("INSERT then asks \"Grid letter <A>:\"", dialog.Note, StringComparison.Ordinal);

                dialog.Justification = "Middle center";
                dialog.Rotation = "90";
                dialog.Height = "3.5";
                dialog.Close(CadDialogAction.Ok);

                Assert.Equal(DxfAttachmentPoint.MiddleCenter, dialog.Request.Justification);
                Assert.Equal(Math.PI / 2.0, dialog.Request.Rotation, 12);
                Assert.Equal(3.5, dialog.Request.Height);
                Assert.Equal(CadDialogAction.Ok, dialog.Request.Action);
            }
        }

        [Fact]
        public void TheEnhancedAttributeEditorShowsTheAttributes_AndOkWritesTheValueAndColourChanged()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                string handle = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
                model.ShowAttributeEditor = editor =>
                {
                    Assert.Equal("GB", editor.Block);
                    Assert.Equal("GRID", editor.Tag);
                    AttributeRowViewModel row = Assert.Single(editor.Rows);
                    Assert.Equal("Grid letter", row.Prompt);
                    Assert.Equal("ByLayer", row.Color);
                    row.Value = "D";
                    row.Color = "Red";
                    row.Height = "5";
                    editor.Tab = "Properties";
                    Assert.True(editor.IsPropertiesTab);
                    editor.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("EATTEDIT");
                model.Session.FeedSelection(new[] { handle });

                DxfAttributeReference attribute = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().AttributeCollection.Single();
                Assert.Equal("D", attribute.TextString);
                Assert.Equal(1, attribute.Color.ColorIndex);
                Assert.Equal(5.0, attribute.Height);
                Assert.Equal("EATTEDIT", model.Document.NextUndo.Name);
            }
        }
    }
}
