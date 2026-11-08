using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class LayoutTests
    {
        /// <summary>
        /// A model of a line 1 000 long and a text on the layer Notes, and a layout of A3 on its side with two viewports onto
        /// it: one 150 by 100 about (100, 100) at 1:20, one 100 by 100 about (300, 150) at 1:5 with Notes frozen in it.
        /// </summary>
        internal static CadDocument TwoViewports()
        {
            var database = new DxfDatabase("AC1018");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            var note = new DxfDBText { TextString = "NOTE", Position = new GeoPoint3(500, 50, 0), Height = 25 };
            database.ModelSpace.AppendEntity(note);
            note.Layer = "Notes";

            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(database, null);
            plan.Initialize();
            AddViewport(plan, new GeoPoint3(100, 100, 0), 150, 100, new GeoPoint2(500, 0), 2000);
            DxfViewport detail = AddViewport(plan, new GeoPoint3(300, 150, 0), 100, 100, new GeoPoint2(500, 50), 500);
            detail.FreezeLayersInViewport(new[] { database.LayerTable["Notes"] });
            return CadDocument.From(database);
        }

        private static DxfViewport AddViewport(DxfLayout layout, GeoPoint3 center, double width, double height, GeoPoint2 view, double viewHeight)
        {
            var viewport = new DxfViewport { CenterPoint = center, Width = width, Height = height };
            layout.BlockTableRecord.AppendEntity(viewport);
            viewport.ViewCenter = view;
            viewport.ViewHeight = viewHeight;
            viewport.On = true;
            return viewport;
        }

        [Fact]
        public void ALayoutWithTwoViewportsShowsEachAtItsScaleAboutItsCentreWithItsFrozenLayersLeftOut()
        {
            CadDocument document = TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");

            DisplayList2 list = document.GetDisplay(plan);

            DisplayViewport2[] viewports = list.Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).ToArray();
            Assert.Equal(2, viewports.Length);
            Assert.Equal(0.05, viewports[0].Scale, 12);
            Assert.Equal(0.2, viewports[1].Scale, 12);
            Assert.Equal(100.0, viewports[0].CenterX, 9);
            Assert.Equal(150.0, viewports[1].CenterY, 9);
            Assert.Equal(500.0, viewports[0].ViewCenterX, 9);
            Assert.Equal(50.0, viewports[1].ViewCenterY, 9);
            Assert.DoesNotContain("Notes", viewports[0].FrozenLayers);
            Assert.Contains("Notes", viewports[1].FrozenLayers);

            // The model is drawn through the viewports alone: the layout holds no model item of its own, and each viewport
            // draws the one display list of model space.
            Assert.Equal(2, list.Count);
            DisplayList2 model = document.GetDisplay(document.ModelSpace);
            Assert.Same(model, viewports[0].Model);
            Assert.Same(model, viewports[1].Model);
        }

        [Fact]
        public void TheModelPointAtAViewportsViewCentreLandsOnItsCentreOnTheSheet()
        {
            CadDocument document = TwoViewports();
            DisplayViewport2 viewport = document.GetDisplay(document.Spaces.Single(s => s.Name == "Plan")).Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).First();

            Affine2 m = viewport.ModelToPaper;

            Assert.Equal(100.0, m.X(500, 0), 9);
            Assert.Equal(100.0, m.Y(500, 0), 9);
            Assert.Equal(100.0 + 50.0, m.X(1500, 0), 9);
        }

        [Fact]
        public void ALayoutsPaperIsItsSheetOfA3AboutItsPlottedAreaAndModelSpaceHasNone()
        {
            CadDocument document = TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");

            DisplayBox paper = plan.PaperBounds;

            Assert.Equal(-7.5, paper.MinX, 9);
            Assert.Equal(-20.0, paper.MinY, 9);
            Assert.Equal(412.5, paper.MaxX, 9);
            Assert.Equal(277.0, paper.MaxY, 9);
            Assert.True(document.ModelSpace.PaperBounds.IsEmpty);
        }
    }
}
