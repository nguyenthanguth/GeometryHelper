using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Quick Select and Find and Replace dialogs of QSELECT and FIND, driven with no window through their requests, the
    /// key Ctrl+Shift+A, and a click with no command taking a group whole.
    /// </summary>
    public class QuickSelectFindTests
    {
        /// <summary>Lines on layers WALLS and DOORS, a text and an MTEXT saying M20, and a group of the first two lines.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DOORS" });
            var first = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALLS" };
            var second = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)) { Layer = "WALLS" };
            db.ModelSpace.AppendEntity(first);
            db.ModelSpace.AppendEntity(second);
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)) { Layer = "DOORS" });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = "M20 x 60", Height = 2.5, Position = new GeoPoint3(0, 200, 0) });
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "{\\fArial|b1;Bolts M20}", Location = new GeoPoint3(0, 300, 0), TextHeight = 2.5 });
            db.NamedObjectsDictionary.SetAt("ACAD_GROUP", new DxfDBDictionary());
            var group = new DxfGroup("Walls", true);
            db.GroupDictionary.SetAt("WALLPAIR", group);
            group.Append(new DxfEntity[] { first, second });
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("select.dxf", Drawing())));
            return model;
        }

        [Fact]
        public void QselectShowsQuickSelect_AndOkSelectsTheLinesOfALayer_SayingHowMany()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                QuickSelectViewModel shown = null;
                string[] types = null;
                model.ShowQuickSelect = dialog =>
                {
                    shown = dialog;
                    types = dialog.ObjectTypes.ToArray();
                    dialog.ObjectType = "Line";
                    dialog.Value = "WALLS";
                    return true;
                };

                model.Execute("QSELECT");

                Assert.Equal(new[] { "Entire drawing" }, shown.ApplyToOptions);
                Assert.Equal(new[] { "Multiple", "Line", "MText", "Text" }, types);
                Assert.Equal("Layer", shown.Property);
                Assert.Equal(new[] { "DOORS", "WALLS" }, shown.Values);
                Assert.True(shown.IsValueListed);

                Assert.Equal("2 item(s) selected.", model.History.Last());
                Assert.Equal(2, model.Document.Selection.Count);
            }
        }

        [Fact]
        public void FindShowsFindAndReplace_ReplaceAllIsOneUndoStep_AndKeepsTheFormattingOfMtext()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowFindReplace = dialog =>
                {
                    dialog.FindWhat = "M20";
                    dialog.WholeWords = true;
                    Assert.True(dialog.FindNext());
                    Assert.Equal(2, dialog.Results.Count);
                    Assert.Equal("Text", dialog.Current.ObjectType);
                    Assert.Equal("2 matches in 2 objects · double-click a row to zoom to it", dialog.Summary);
                    dialog.ReplaceWith = "M24";
                    Assert.True(dialog.ReplaceAll().Succeeded);
                    Assert.Empty(dialog.Results);
                };

                model.Execute("FIND");

                Assert.Equal("2 match(es) replaced.", model.History.Last());
                Assert.Equal("FIND", model.Document.NextUndo.Name);
                Assert.Equal("{\\fArial|b1;Bolts M24}", model.Document.ModelSpace.Block.OfType<DxfMText>().Single().Contents);
                Assert.True(model.Document.Undo());
                Assert.Equal("M20 x 60", model.Document.ModelSpace.Block.OfType<DxfDBText>().Single().TextString);
            }
        }

        [Fact]
        public void CtrlShiftA_SwitchesGroupPicking_AndSelectionCyclingIsSwitchedOff()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                CadDocument document = model.Document;

                // A click on the first wall takes the group of both walls.
                Assert.NotNull(CadPicking.Pick(document, document.ModelSpace, 50, 0, 1.0, false));
                Assert.Equal(2, document.Selection.Count);
                document.Selection.Clear();

                Assert.True(model.RunKey("Ctrl+Shift+A"));
                Assert.Equal("<Group off>", model.History.Last());
                CadPicking.Pick(document, document.ModelSpace, 50, 0, 1.0, false);
                Assert.Equal(1, document.Selection.Count);

                model.Session.ToggleSelectionCycling();
                Assert.Equal("<Selection Cycling off>", model.History.Last());
                Assert.Equal(0, document.SelectionCycling);
            }
        }

        [Fact]
        public void ObjectsOverlappingAtAPick_AreListed_AndShiftSpaceCyclesThroughThem()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            var across = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)) { Layer = "WALLS" };
            var up = new DxfLine(new GeoPoint3(50, 0, 0), new GeoPoint3(50, 100, 0));
            db.ModelSpace.AppendEntity(across);
            db.ModelSpace.AppendEntity(up);
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 10));
            CadDocument document = CadDocument.From(db, "cross.dxf");
            var at = new GeoPoint2(50, 50);

            // Two lines cross at the pick: the list holds both, the one drawn on top first, with their layers; one object alone is no list.
            Assert.Equal(new[] { up.Handle, across.Handle }, CadPicking.Overlapping(document, document.ModelSpace, null, at, 1.0));
            Assert.Equal("Line · WALLS", CadPicking.CycleLabel(document, across.Handle));
            Assert.Empty(CadPicking.Overlapping(document, document.ModelSpace, null, new GeoPoint2(510, 500), 1.0));
            Assert.True(CadPicking.Choose(document, null, up.Handle, false));
            Assert.Equal(new[] { up.Handle }, document.Selection.Handles);
            document.Selection.Clear();

            // With SELECTIONCYCLING 1 there is no list; Shift+Space armed, each click at the place takes the next.
            document.SelectionCycling = 1;
            Assert.Empty(CadPicking.Overlapping(document, document.ModelSpace, null, at, 1.0));
            var cycler = new CadSelectionCycler();
            cycler.Arm();
            Assert.Equal(up.Handle, cycler.Next(document, document.ModelSpace, at, 1.0));
            Assert.Equal(across.Handle, cycler.Next(document, document.ModelSpace, at, 1.0));
            Assert.Equal(new[] { across.Handle }, document.Selection.Handles);
            Assert.Equal(up.Handle, cycler.Next(document, document.ModelSpace, at, 1.0));
            Assert.Equal(new[] { up.Handle }, document.Selection.Handles);
        }
    }
}
