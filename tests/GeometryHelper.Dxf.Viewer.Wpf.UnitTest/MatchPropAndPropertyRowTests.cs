using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>MATCHPROP's Property Settings dialog and the rows of Properties that offer a list, driven through the view model.</summary>
    public class MatchPropAndPropertyRowTests
    {
        private static CadDocument Drawing(params DxfEntity[] entities)
        {
            var database = new DxfDatabase("AC1027");

            foreach (DxfEntity entity in entities)
            {
                database.ModelSpace.AppendEntity(entity);
            }

            return CadDocument.From(database, "plan.dxf");
        }

        [Fact]
        public void TheSettingsOfMatchProp_ShowItsDialog_AndOkLeavesOnlyTheBoxesChecked()
        {
            var red = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(100, 0, 0), Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), LinetypeScale = 2 };
            var plain = new DxfLine { StartPoint = new GeoPoint3(0, 50, 0), EndPoint = new GeoPoint3(100, 50, 0) };
            CadDocument document = Drawing(red, plain);
            var model = new MainViewModel();
            model.OpenDocument(document, "plan.dxf");
            MatchPropSettingsViewModel shown = null;
            model.ShowMatchPropSettings = dialog =>
            {
                shown = dialog;
                dialog.Basic.Single(b => b.Name == "Color").IsChecked = false;
                dialog.Special.Single(b => b.Name == "Material").IsChecked = true;
                return true;
            };

            model.StartCommand("MATCHPROP");
            model.Session.FeedSelection(new[] { red.Handle });
            model.Execute("S");
            model.Session.FeedSelection(new[] { plain.Handle });
            model.Enter();

            Assert.NotNull(shown);
            Assert.False(shown.Basic.Single(b => b.Name == "Plot Style").IsEnabled);
            Assert.False(shown.Special.Single(b => b.Name == "Material").IsChecked);
            Assert.Equal(CadMatchProperties.All & ~CadMatchProperties.Color, model.Session.MatchProperties);
            Assert.True(plain.Color.IsByLayer);
            Assert.Equal(2.0, plain.LinetypeScale);
        }

        [Fact]
        public void CancelOfTheSettings_KeepsThoseMatchPropHad()
        {
            CadDocument document = Drawing(new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(100, 0, 0) });
            var model = new MainViewModel();
            model.OpenDocument(document, "plan.dxf");
            model.ShowMatchPropSettings = dialog =>
            {
                dialog.Basic.Single(b => b.Name == "Layer").IsChecked = false;
                return false;
            };

            model.StartCommand("MATCHPROP");
            model.Session.FeedSelection(new[] { document.ModelSpace.Block.Cast<DxfEntity>().First().Handle });
            model.Execute("S");

            Assert.Equal(CadMatchProperties.All, model.Session.MatchProperties);
        }

        [Fact]
        public void ARowOfAList_OffersItsChoices_AndAnMTextsContentsOpenItsEditor()
        {
            var mtext = new DxfMText { Contents = "NOTE", TextHeight = 2.5, Location = new GeoPoint3(0, 0, 0) };
            CadDocument document = Drawing(mtext);
            var model = new MainViewModel();
            model.OpenDocument(document, "plan.dxf");
            document.Selection.Set(new[] { mtext.Handle });

            PropertyRowViewModel justify = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Justify");
            Assert.True(justify.HasOptions);
            Assert.Contains("Middle center", justify.Options);

            justify.Value = "Middle center";
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, mtext.Attachment);

            PropertyRowViewModel contents = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Contents");
            Assert.Contains(MainViewModel.EditMTextOption, contents.Options);
            contents.Value = MainViewModel.EditMTextOption;

            Assert.Equal("NOTE", contents.Value);
            Assert.Equal("TEXTEDIT", model.Session.Current?.Name);
        }
    }
}
