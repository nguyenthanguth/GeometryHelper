using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Object Selection Filters dialog with a Transparency line (Select... lists ByLayer, ByBlock and the per cents in use,
    /// a line edited comes back as it was written), and QPLOCATION, which AutoCAD 2021 starts at 0, takes 0 or 1 only and says
    /// "Requires 0 or 1 only." of anything else.
    /// </summary>
    public class FilterTransparencyQplocationTests
    {
        private static SelectionFiltersViewModel Dialog()
        {
            var db = new DxfDatabase("AC1027");
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            line.Transparency = new DxfTransparency(128);
            var other = new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0));
            db.ModelSpace.AppendEntity(other);
            other.Transparency = new DxfTransparency(76);
            return new SelectionFiltersViewModel(CadDocument.From(db, "dialog.dxf"), new CadSelectionFilter());
        }

        private static CadFilterChoice Pick(string name) => CadSelectionFilter.Choices.First(c => c.Name == name);

        [Fact]
        public void ChoosingTransparency_ListsByLayerByBlockAndThePerCentsInUse_AndAddsALineWithAnOperator()
        {
            SelectionFiltersViewModel dialog = Dialog();
            dialog.Choice = Pick("Transparency");

            Assert.True(dialog.TakesOperators);
            Assert.False(dialog.IsPoint);
            Assert.Equal(new[] { "ByLayer", "ByBlock", "50", "70" }, dialog.Values);

            dialog.OperatorX = ">=";
            dialog.Value = "60";
            Assert.True(dialog.AddToList());
            Assert.Equal("Transparency         >= 60", dialog.Lines[0]);
            Assert.True(dialog.TryApply());
        }

        [Fact]
        public void ATransparencyLineEdited_ComesBackAsWritten_NotAsANumberWithFourPlaces()
        {
            SelectionFiltersViewModel dialog = Dialog();
            dialog.Choice = Pick("Transparency");
            dialog.Value = "ByBlock";
            Assert.True(dialog.AddToList());
            dialog.SelectedLine = 0;

            dialog.EditItem();

            Assert.Equal("ByBlock", dialog.Value);
        }

        [Fact]
        public void ATransparencyThatIsNoPerCent_IsRefusedAsAnInvalidValue()
        {
            SelectionFiltersViewModel dialog = Dialog();
            dialog.Choice = Pick("Transparency");
            dialog.Value = "95";

            Assert.False(dialog.AddToList());
            Assert.Equal("Invalid Value.", dialog.Message);
        }

        [Fact]
        public void Qplocation_StartsAtNought_TakesNoughtOrOne_AndRefusesTheRest()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("qp.dxf", new DxfDatabase("AC1027"))));
                Assert.Equal(0, model.QuickPropertiesLocation);

                model.Execute("QPLOCATION");
                model.Execute("5");
                Assert.Equal(
                    "Requires 0 or 1 only.",
                    model.History.Last(h => h.StartsWith("Requires", System.StringComparison.Ordinal))
                );
                model.Execute("1");

                Assert.Equal(1, model.QuickPropertiesLocation);
            }
        }
    }
}
