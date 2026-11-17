using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The contextual Array tab: shown while one associative array is selected, its values those of the array's parameters, and
    /// each value written one undo step named ARRAYEDIT that makes the items again.
    /// </summary>
    public sealed class ArrayTabTests
    {
        /// <summary>A drawing of R2013 with a rectangular associative array of a circle: 3 columns 20 apart, 4 rows 30 apart.</summary>
        private static MainViewModel WithArray(out string insert, out string loose)
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(10, 10, 0), new GeoVector3(0, 0, 1), 5);
            var other = new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(circle);
            database.ModelSpace.AppendEntity(other);
            loose = other.Handle;
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "array.dxf"));
            model.Document.Selection.Set(new[] { circle.Handle });

            foreach (string input in new[] { "ARRAYRECT", "AS", "Y", "COU", "3", "4", "S", "20", "30", "X" })
            {
                model.Execute(input);
            }

            insert = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
            model.Document.Selection.Clear();
            return model;
        }

        [Fact]
        public void TheArrayTabIsShownWhileOneAssociativeArrayIsSelected_AndHiddenForAnyOtherObject()
        {
            MainViewModel model = WithArray(out string insert, out string loose);
            Assert.False(model.ArrayTab.IsTabVisible);
            Assert.Null(model.ArrayEditor);

            model.Document.Selection.Set(new[] { insert });

            Assert.True(model.ArrayTab.IsTabVisible);
            Assert.Equal("Array", model.ArrayTab.TabHeader);
            Assert.Same(model.ArrayTab, model.SelectedTab);
            ArrayTabViewModel tab = model.ArrayEditor;
            Assert.NotNull(tab);
            Assert.Equal("Rectangular", tab.TypeName);
            Assert.True(tab.IsRectangular);
            Assert.Equal("Columns", tab.ItemsTitle);
            Assert.Equal(3, tab.Items);
            Assert.Equal(20.0, tab.Between, 9);
            Assert.Equal(40.0, tab.Total, 9);
            Assert.Equal(4, tab.Rows);
            Assert.Equal(30.0, tab.RowBetween, 9);
            Assert.Equal(90.0, tab.RowTotal, 9);
            Assert.Equal(1, tab.Levels);

            model.Document.Selection.Set(new[] { loose });

            Assert.False(model.ArrayTab.IsTabVisible);
            Assert.Null(model.ArrayEditor);
        }

        [Fact]
        public void AValueWrittenInTheTab_ChangesTheArray_OneUndoStepNamedArrayEdit()
        {
            MainViewModel model = WithArray(out string insert, out _);
            model.Document.Selection.Set(new[] { insert });
            ArrayTabViewModel tab = model.ArrayEditor;
            string start = model.Document.Database.ToText();

            tab.Items = 5;
            Assert.Equal("ARRAYEDIT", model.Document.NextUndo.Name);
            Assert.Equal(5, tab.Items);
            tab.Total = 160.0;
            Assert.Equal(40.0, tab.Between, 9);
            tab.Rows = 2;
            tab.RowBetween = 25.0;
            tab.Levels = 2;
            tab.LevelBetween = 7.0;

            Assert.Equal(5, tab.Items);
            Assert.Equal(40.0, tab.Between, 9);
            Assert.Equal(2, tab.Rows);
            Assert.Equal(25.0, tab.RowBetween, 9);
            Assert.Equal(2, tab.Levels);
            Assert.Equal(7.0, tab.LevelBetween, 9);

            // 5 columns, 2 rows and 2 levels: 20 items in the block of the INSERT.
            Assert.Equal(
                20,
                model
                    .Document.ModelSpace.Block.OfType<DxfBlockReference>()
                    .Single()
                    .BlockTableRecord.OfType<DxfBlockReference>()
                    .Count()
            );
            Assert.Equal(insert, model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle);

            while (model.Document.CanUndo && model.Document.NextUndo.Name == "ARRAYEDIT")
            {
                Assert.True(model.Document.Undo());
            }

            Assert.Equal(start, model.Document.Database.ToText());
        }

        [Fact]
        public void ResetArrayMakesTheItemsAgain_AndAnUndoneArrayLeavesTheTab()
        {
            MainViewModel model = WithArray(out string insert, out _);
            model.Document.Selection.Set(new[] { insert });
            ArrayTabViewModel tab = model.ArrayEditor;

            tab.ResetCommand.Execute(null);

            Assert.Equal("ARRAYEDIT", model.Document.NextUndo.Name);
            Assert.Equal(3, tab.Items);
            Assert.Equal(
                12,
                model
                    .Document.ModelSpace.Block.OfType<DxfBlockReference>()
                    .Single()
                    .BlockTableRecord.OfType<DxfBlockReference>()
                    .Count()
            );
        }
    }
}
