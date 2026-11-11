using System.IO;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The drafting toggles AutoCAD 2021 keeps in the drawing: SNAP and GRID with their spacing from the <c>*Active</c>
    /// viewport, ORTHO from <c>$ORTHOMODE</c> and the lineweights from <c>$LWDISPLAY</c>, each drawing's own shown as its tab
    /// is, written into it when switched, and kept by a save.
    /// </summary>
    public class DrawingDraftingToggleTests
    {
        /// <summary>The room with SNAP, GRID and ORTHO on, the lineweights off, a snap of 2.5 by 5 and a grid of 10 by 20.</summary>
        private static DxfDatabase SetRoom()
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapEnabled = true;
            active.GridEnabled = true;
            active.SnapIncrements = new GeoVector2(2.5, 5.0);
            active.GridIncrements = new GeoVector2(10.0, 20.0);
            db.Orthomode = true;
            db.SetHeader("$LWDISPLAY", new DxfTypedValue(290, "0"));
            return db;
        }

        private static bool On(MainViewModel model, string name) => model.Toggles.Single(t => t.Name == name).IsOn;

        private static void Show(MainViewModel model, string label) =>
            model.ActivateFileCommand.Execute(model.FileRows.Single(r => r.Label == label));

        [Fact]
        public void EachDrawingOpened_ShowsItsOwnSnapGridOrthoAndLineweights_AsItsTabIsShown()
        {
            using (var folder = new TestFolder())
            {
                string set = folder.Write("set.dxf", SetRoom());
                string plain = folder.Write("plain.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();

                model.Open(set);
                Assert.True(On(model, "SNAP"));
                Assert.True(On(model, "GRID"));
                Assert.True(On(model, "ORTHO"));
                Assert.True(model.Snap.GridSnap);
                Assert.True(model.Snap.Ortho);
                Assert.True(model.GridOn);
                Assert.Equal(2.5, model.Snap.GridX);
                Assert.Equal(5.0, model.Snap.GridY);
                Assert.Equal(10.0, model.GridSpacingX);
                Assert.Equal(20.0, model.GridSpacingY);
                Assert.False(model.ShowLineWeights);
                model.Execute("LWDISPLAY");
                Assert.Equal("Enter new value for LWDISPLAY <0>:", model.Prompt);
                model.Execute("");

                // A drawing of the template's *Active, SNAPMODE and GRIDMODE 0, and no $ORTHOMODE or $LWDISPLAY: the window's.
                model.Open(plain);
                Assert.False(On(model, "SNAP"));
                Assert.False(On(model, "GRID"));
                Assert.False(On(model, "ORTHO"));
                Assert.Equal(10.0, model.Snap.GridX);
                Assert.Equal(10.0, model.GridSpacingY);
                Assert.True(model.ShowLineWeights);

                // POLAR, OTRACK and OSNAP are the window's, kept across the tabs.
                model.Toggles.Single(t => t.Name == "POLAR").IsOn = false;
                Show(model, "set.dxf");
                Assert.True(On(model, "SNAP"));
                Assert.True(On(model, "GRID"));
                Assert.True(On(model, "ORTHO"));
                Assert.False(On(model, "POLAR"));
                Assert.Equal(2.5, model.Snap.GridX);
                Assert.Equal(20.0, model.GridSpacingY);
                Assert.False(model.ShowLineWeights);

                // Showing a drawing's own writes nothing into either.
                Assert.False(model.Document.IsModified);
            }
        }

        [Fact]
        public void TogglesSwitched_AreWrittenIntoTheDrawingShownAlone_AndASaveAndReopenKeepsThem()
        {
            using (var folder = new TestFolder())
            {
                string first = folder.Write("first.dxf", TestFiles.Room(out _, out _));
                string second = folder.Write("second.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();
                model.Open(first);
                CadDocument firstDocument = model.Document;
                model.Open(second);

                model.Execute("GRIDMODE");
                model.Execute("1");
                model.Execute("SNAP");
                model.Execute("2.5");
                model.Execute("ORTHOMODE");
                model.Execute("1");
                model.Execute("LWDISPLAY");
                model.Execute("0");

                DxfDatabase written = model.Document.Database;
                Assert.True(written.ViewportTable["*Active"].GridEnabled);
                Assert.True(written.ViewportTable["*Active"].SnapEnabled);
                Assert.Equal(new GeoVector2(2.5, 2.5), written.ViewportTable["*Active"].SnapIncrements);
                Assert.True(written.Orthomode);
                Assert.Equal("0", written.GetHeader("$LWDISPLAY").Single().Value.Trim());
                Assert.False(model.ShowLineWeights);

                // The other drawing keeps its own, and shows them again.
                Show(model, "first.dxf");
                Assert.False(On(model, "GRID"));
                Assert.False(On(model, "SNAP"));
                Assert.False(On(model, "ORTHO"));
                Assert.Equal(10.0, model.Snap.GridX);
                Assert.True(model.ShowLineWeights);
                Assert.False(firstDocument.Database.ViewportTable["*Active"].GridEnabled);
                Assert.False(firstDocument.Database.Orthomode);
                Assert.Null(firstDocument.Database.GetHeader("$LWDISPLAY"));

                Show(model, "second.dxf");
                string saved = Path.Combine(folder.FullPath, "saved.dxf");
                Assert.True(model.SaveAs(saved));

                var again = new MainViewModel();
                again.Open(saved);
                Assert.True(On(again, "GRID"));
                Assert.True(On(again, "SNAP"));
                Assert.True(On(again, "ORTHO"));
                Assert.Equal(2.5, again.Snap.GridX);
                Assert.Equal(2.5, again.Snap.GridY);
                Assert.False(again.ShowLineWeights);
            }
        }

        [Fact]
        public void ANewDrawing_ShowsTheGridGlobalSettingsSay_WhichADrawingOfItsOwnGridThenKeeps()
        {
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { Grid = true });

            model.NewDrawing();
            Assert.True(model.GridOn);
            Assert.True(model.Document.Database.ViewportTable["*Active"].GridEnabled);

            model.ApplySettings(new GlobalSettings { Grid = false });
            Assert.True(model.GridOn);
        }
    }
}
