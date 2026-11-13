using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Object Selection Filters dialog with the items of AutoCAD 2021's Select Filter list: value tests with an operator
    /// and X, Y and Z, the begin and end of AND, OR, XOR and NOT, and Apply refusing groups that are not well formed.
    /// </summary>
    public class SelectionFiltersDialogTests
    {
        private static SelectionFiltersViewModel Dialog(CadSelectionFilter filter)
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(300, 0, 0), GeoVector3.ZAxis, 25));
            return new SelectionFiltersViewModel(CadDocument.From(db, "dialog.dxf"), filter);
        }

        private static CadFilterChoice Pick(string name) => CadSelectionFilter.Choices.First(c => c.Name == name);

        [Fact]
        public void ChoosingAKindOfObject_SetsAnObjectLineToIt()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("Arc");

            Assert.Equal(CadFilterProperty.Object, dialog.Property);
            Assert.Equal("Arc", dialog.Value);
            Assert.True(dialog.AddToList());
            Assert.Equal(new[] { "Object               = Arc" }, dialog.Lines);
        }

        [Fact]
        public void ChoosingANumber_ShowsOperatorsAndOnlyX_AndAPoint_ShowsYAndZToo()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("Circle Radius");
            Assert.True(dialog.TakesOperators);
            Assert.False(dialog.IsPoint);
            Assert.Equal(new[] { "=", "!=", "<", "<=", ">", ">=" }, dialog.Operators);

            dialog.OperatorX = ">=";
            dialog.Value = "15";
            Assert.True(dialog.AddToList());
            Assert.Equal("Circle Radius        >= 15.0000", dialog.Lines[0]);

            dialog.Choice = Pick("Circle Center");
            Assert.True(dialog.IsPoint);
            Assert.Equal("*", dialog.Operators.Last());
            dialog.OperatorX = ">=";
            dialog.Value = "100";
            dialog.OperatorY = "*";
            dialog.ValueY = "0";
            dialog.OperatorZ = "<";
            dialog.ValueZ = "5";
            Assert.True(dialog.AddToList());
            Assert.Equal("Circle Center        X >= 100.0000  Y * 0.0000  Z < 5.0000", dialog.Lines[1]);

            dialog.Choice = Pick("Layer");
            Assert.False(dialog.TakesOperators);
            Assert.False(dialog.IsPoint);
        }

        [Fact]
        public void ANumberThatIsNotOne_IsRefusedInAutoCADsWords()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("Arc Radius");
            dialog.Value = "ten";
            Assert.False(dialog.AddToList());
            Assert.Equal("Invalid Number.", dialog.Message);

            dialog.Choice = Pick("Line End");
            dialog.Value = "1";
            dialog.ValueY = "x";
            Assert.False(dialog.AddToList());
            Assert.Equal("Invalid Y coordinate.", dialog.Message);
        }

        [Fact]
        public void AGroupsBeginAndEnd_AreAddedAsLinesWithNoValue()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("** Begin   NOT");
            Assert.True(dialog.IsGroup);
            Assert.True(dialog.AddToList());
            dialog.Choice = Pick("** End      NOT");
            Assert.True(dialog.AddToList());

            Assert.Equal(new[] { "** Begin   NOT", "** End      NOT" }, dialog.Lines);
        }

        [Fact]
        public void Apply_RefusesGroupsThatAreNotWellFormed_AndGivesTheFilterNothing()
        {
            var filter = new CadSelectionFilter();
            SelectionFiltersViewModel dialog = Dialog(filter);
            dialog.Choice = Pick("** Begin   OR");
            Assert.True(dialog.AddToList());

            Assert.False(dialog.TryApply());
            Assert.Equal("Invalid Filter list - Premature end.", dialog.Message);
            Assert.Empty(filter.Lines);

            dialog.Choice = Pick("Circle Radius");
            dialog.Value = "25";
            Assert.True(dialog.AddToList());
            dialog.Choice = Pick("** End      OR");
            Assert.True(dialog.AddToList());

            Assert.True(dialog.TryApply());
            Assert.Null(dialog.Message);
            Assert.Equal(3, filter.Lines.Count);
        }

        [Fact]
        public void EditItem_PutsAPointsOperatorsAndCoordinatesBack()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("Line Start");
            dialog.OperatorX = ">";
            dialog.Value = "1.5";
            dialog.OperatorY = "*";
            dialog.ValueY = "2";
            dialog.OperatorZ = "<=";
            dialog.ValueZ = "3";
            Assert.True(dialog.AddToList());

            dialog.Choice = Pick("Layer");
            dialog.SelectedLine = 0;
            dialog.EditItem();

            Assert.Equal(CadFilterProperty.LineStart, dialog.Property);
            Assert.Equal(">", dialog.OperatorX);
            Assert.Equal("1.5", dialog.Value);
            Assert.Equal("*", dialog.OperatorY);
            Assert.Equal("2", dialog.ValueY);
            Assert.Equal("<=", dialog.OperatorZ);
            Assert.Equal("3", dialog.ValueZ);
        }

        [Fact]
        public void Values_OfABlockNameAndAStyle_AreTheDrawings()
        {
            SelectionFiltersViewModel dialog = Dialog(new CadSelectionFilter());
            dialog.Choice = Pick("Text Style Name");
            Assert.Contains("Standard", dialog.Values);
            dialog.Choice = Pick("Dimension Style");
            Assert.Contains("Standard", dialog.Values);
            dialog.Choice = Pick("Arc Radius");
            Assert.Empty(dialog.Values);
        }
    }
}
