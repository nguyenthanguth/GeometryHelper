using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// What C13 left of the block dialogs, driven with no window: the Enhanced Attribute Editor showing a value as it is typed,
    /// Apply keeping it open and Cancel taking back the rest; Block Definition's picture of the objects selected; the question
    /// whether a block of another drawing redefines the drawing's own; and the ghost of the block INSERT drags.
    /// </summary>
    public class BlockEditingDialogTests
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

        /// <summary>Makes the block GB of the bubble through the dialog, the objects selected first, its base point (100, 70); its INSERT takes their place.</summary>
        private static void MakeBubble(MainViewModel model)
        {
            model.ShowBlockDefinition = dialog =>
            {
                dialog.Name = "GB";
                dialog.BaseX = "100";
                dialog.BaseY = "70";
                dialog.Close(CadDialogAction.Ok);
                return true;
            };
            model.Execute("SELECTALL");
            model.Execute("B");
        }

        private static string Shown(MainViewModel model) => model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().AttributeCollection.Single().TextString;

        [Fact]
        public void TheEditorShowsAValueAsItIsTyped_ApplyKeepsItWithTheEditorOpen_AndCancelTakesBackWhatCameAfter()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                string handle = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
                model.ShowAttributeEditor = editor =>
                {
                    AttributeRowViewModel row = Assert.Single(editor.Rows);
                    row.Value = "Q";
                    Assert.Equal("Q", Shown(model));

                    editor.Apply();
                    row.Value = "R";
                    Assert.Equal("R", Shown(model));
                    row.Height = "0";
                    Assert.Equal("R", Shown(model));

                    editor.Close(CadDialogAction.Cancel);
                    return true;
                };

                model.Execute("EATTEDIT");
                model.Session.FeedSelection(new[] { handle });

                Assert.False(model.Session.IsRunning);
                Assert.Equal("Q", Shown(model));
                Assert.Equal("EATTEDIT", model.Document.NextUndo.Name);
                Assert.True(model.Document.Undo());
                Assert.Equal("A", Shown(model));
            }
        }

        /// <summary>The number of pixels of a picture that are not black.</summary>
        private static int Lit(ImageSource picture)
        {
            var bitmap = new FormatConvertedBitmap((BitmapSource)picture, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            int lit = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                lit += pixels[i] > 60 || pixels[i + 1] > 60 || pixels[i + 2] > 60 ? 1 : 0;
            }

            return lit;
        }

        [Fact]
        public void BlockDefinitionDrawsTheObjectsSelected_ForANewName_AndLeavesTheDrawingAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string before = model.Document.Database.ToText();
                var request = new CadBlockDefinitionRequest(model.Document)
                {
                    Objects = model.Document.ModelSpace.Block.Select(e => e.Handle).ToList(),
                    BasePoint = new GeoPoint3(100, 70, 0),
                };
                var dialog = new BlockDefinitionViewModel(request) { Name = "NEW" };

                Assert.False(dialog.Exists);
                Assert.NotNull(dialog.Preview);
                Assert.True(Lit(dialog.Preview) > 20, "lit pixels: " + Lit(dialog.Preview));
                Assert.Equal(before, model.Document.Database.ToText());

                dialog.SpecifyObjectsOnScreen = true;
                Assert.Null(dialog.Preview);
                Assert.Null(new BlockDefinitionViewModel(new CadBlockDefinitionRequest(model.Document)).Preview);
            }
        }

        /// <summary>Saves a drawing whose block GB is one line from (0, 0) to (40, 0), and gives its path.</summary>
        private static string OtherGb(TestFolder folder)
        {
            DxfDatabase other = CadTemplate.CreateMetric();
            var record = new DxfBlockTableRecord { Name = "GB" };
            other.BlockTable.Add(record);
            record.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            string path = Path.Combine(folder.FullPath, "other.dxf");
            other.Save(path);
            return path;
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 3)]
        public void AnotherDrawingsBlockOfAUsedName_AsksWhetherToRedefine_AndTheAnswerDecides(bool redefine, int entities)
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                string other = OtherGb(folder);
                string asked = null;
                model.AskRedefineBlock = name =>
                {
                    asked = name;
                    return redefine;
                };
                model.ShowBlocksPalette = palette =>
                {
                    Assert.True(palette.OpenOther(other));
                    Assert.True(palette.Choose(palette.Tiles.Single(t => t.Name == "GB")));
                    return true;
                };

                model.Execute("I");
                model.Execute("10,20");

                if (!redefine)
                {
                    model.Execute(string.Empty);
                }

                Assert.Equal("GB", asked);
                Assert.False(model.Session.IsRunning);
                Assert.Equal(entities, model.Document.Database.BlockTable["GB"].Count);
                Assert.Equal(2, model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Count());
            }
        }

        [Fact]
        public void TheGhostOfTheBlockInsertDrags_OutlinesItsCircleAndLine_InTheBlocksOwnCoordinates()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                MakeBubble(model);
                model.Execute("-I");
                model.Execute("GB");

                CadBlockDrag drag = model.Session.Prompt.Drag;
                Assert.NotNull(drag);
                IReadOnlyList<double[]> paths = new CadGhost().Paths(drag, 0.01);

                Assert.True(paths.Count >= 2, "paths: " + paths.Count);
                Assert.All(paths, xy =>
                {
                    for (int i = 0; i + 1 < xy.Length; i += 2)
                    {
                        Assert.InRange(xy[i], 90 - 1E-6, 110 + 1E-6);
                        Assert.InRange(xy[i + 1], 70 - 1E-6, 110 + 1E-6);
                    }
                });
            }
        }
    }
}
