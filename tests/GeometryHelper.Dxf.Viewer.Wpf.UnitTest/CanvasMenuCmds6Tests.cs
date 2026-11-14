using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas menu items AutoCAD 2021 wires to HATCHSETORIGIN (Set Origin of a hatch), TABLEEXPORT (Export of a table) and
    /// VPMAX (Maximize Viewport of a viewport), enabled and running the command on the object selected; the Table Style items
    /// of the table menu, which need a starting table in the table style, stay grey.
    /// </summary>
    public sealed class CanvasMenuCmds6Tests
    {
        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        [Fact]
        public void SetOriginOfAHatch_StartsHatchsetoriginOnIt_AndAsksTheOriginAtOnce()
        {
            var database = new DxfDatabase("AC1027");

            foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3), (3, 0) })
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(100, 60, 0),
                    new GeoPoint3(0, 60, 0),
                };
                database.ModelSpace.AppendEntity(new DxfLine(corners[a], corners[b]));
            }

            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "origin.dxf"));
            model.Execute("HATCH");
            model.Execute("5,5");
            model.Execute(string.Empty);
            DxfHatch hatch = model.Document.ModelSpace.Block.OfType<DxfHatch>().Single();
            model.Document.Selection.Set(new[] { hatch.Handle });

            CadMenuItemViewModel set = Item(model.CanvasMenu(), "Set Origin");
            Assert.True(set.IsEnabled);
            set.Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Equal("HATCHSETORIGIN", model.Session.Current.Name);
            Assert.Equal("Select new hatch origin:", model.Session.PromptText);

            model.Execute("10,5");

            Assert.False(model.IsCommandRunning);
            DxfPatternDefinition line = model
                .Document.ModelSpace.Block.OfType<DxfHatch>()
                .Single()
                .GetPatternDefinitionAt(0);
            Assert.Equal(10.0, line.BaseX, 9);
            Assert.Equal(5.0, line.BaseY, 9);
        }

        [Fact]
        public void ExportOfATable_StartsTableexportOnIt_AndAsksTheFileOnTheCommandLineWithNoWindow()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), "table.dxf"));
            model.ShowInsertTable = dialog =>
            {
                dialog.Columns = "2";
                dialog.ColumnWidth = "40";
                dialog.DataRows = "2";
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            string handle = model.Document.ModelSpace.Block.OfType<DxfTable>().Single().Handle;
            model.Document.Selection.Set(new[] { handle });

            CadMenuItemViewModel export = Item(model.CanvasMenu(), "Export...");
            Assert.True(export.IsEnabled);
            export.Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Equal("TABLEEXPORT", model.Session.Current.Name);
            Assert.StartsWith("Enter file name <", model.Session.PromptText);
        }

        [Fact]
        public void TheTableStyleItems_StayGrey_ForWantOfAStartingTableInTheStyle()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), "table.dxf"));
            model.ShowInsertTable = dialog =>
            {
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            model.Document.Selection.Set(new[] { model.Document.ModelSpace.Block.OfType<DxfTable>().Single().Handle });

            CadMenuItemViewModel style = Item(model.CanvasMenu(), "Table Style");

            Assert.All(style.Items, i => Assert.False(i.IsEnabled));
        }

        [Fact]
        public void MaximizeViewport_StartsVpmaxOnTheViewportSelected_AndTheViewportTakesTheSheet()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.OpenDocument(
                    CadDocument.From(TestFiles.RoomWithViewport(folder, out _, out string viewport), "sheet.dxf")
                );
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                model.Document.Selection.Set(new[] { viewport });
                double width = ((DxfViewport)model.Document.FindEntity(viewport)).Width;

                Item(model.CanvasMenu(), "Maximize Viewport").Command.Execute(null);

                Assert.False(model.IsCommandRunning);
                Assert.Equal(viewport, model.Document.MaximizedViewportHandle);
                Assert.True(((DxfViewport)model.Document.FindEntity(viewport)).Width > width);

                model.Execute("VPMIN");

                Assert.False(model.Document.IsViewportMaximized);
                Assert.Equal(width, ((DxfViewport)model.Document.FindEntity(viewport)).Width, 9);
            }
        }
    }
}
