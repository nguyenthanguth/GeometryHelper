using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// DesignCenter driven with no window through its view model: the tree of folders, drawings and kinds of content, the list
    /// on the right, Back, Forward and Up, the History and Open Drawings tabs, the menu and double click that insert a block or
    /// copy a layer into the drawing shown, and a drag dropped on the canvas.
    /// </summary>
    public class DesignCenterViewModelTests
    {
        /// <summary>
        /// A folder holding library.dxf (layers ROAD and WALL, the block TREE of a circle on ROAD, the linetype GAS), the folder
        /// sub, a note and plan.dxf, which is opened in the presenter.
        /// </summary>
        private static MainViewModel Site(TestFolder folder, out string library)
        {
            DxfDatabase source = CadTemplate.CreateMetric();
            var gas = new DxfLinetypeTableRecord
            {
                Name = "GAS",
                AsciiDescription = "Gas ---- GAS ----",
                NumDashes = 2,
            };
            gas.SetDashLengthAt(0, 12.0);
            gas.SetDashLengthAt(1, -6.0);
            gas.PatternLength = 18.0;
            source.LinetypeTable.Add(gas);
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "ROAD", ColorIndex = 3 });
            source.LayerTable["ROAD"].LinetypeName = "GAS";
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 1 });
            var tree = new DxfBlockTableRecord
            {
                Name = "TREE",
                Origin = new GeoPoint3(0, 0, 0),
                Comments = "Tree 3 m",
            };
            source.BlockTable.Add(tree);
            tree.AppendEntity(
                new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1500.0) { Layer = "ROAD" }
            );
            library = folder.Write("library.dxf", source);
            Directory.CreateDirectory(Path.Combine(folder.FullPath, "sub"));
            folder.WriteText("notes.txt", "not a drawing");

            DxfDatabase plan = CadTemplate.CreateMetric();
            plan.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 5 });
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan.dxf", plan)));
            return model;
        }

        private static string[] Names(IEnumerable<DesignCenterItemViewModel> items) =>
            items.Select(i => i.Name).ToArray();

        [Fact]
        public void TheFoldersTab_ListsTheFoldersAndDrawingsOfARoot_InOrder_LeavingOutWhatIsNotADrawing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });

                DesignCenterNode root = Assert.Single(palette.Roots);
                root.IsSelected = true;
                root.IsExpanded = true;

                Assert.Equal(new[] { "sub", "library.dxf", "plan.dxf" }, root.Children.Select(c => c.Text));
                Assert.Equal(
                    new[] { DesignCenterNodeKind.Folder, DesignCenterNodeKind.Drawing, DesignCenterNodeKind.Drawing },
                    root.Children.Select(c => c.Kind)
                );
                Assert.Equal(new[] { "sub", "library.dxf", "plan.dxf" }, Names(palette.Items));
                Assert.Equal(
                    new[] { DesignCenterItemKind.Folder, DesignCenterItemKind.Drawing, DesignCenterItemKind.Drawing },
                    palette.Items.Select(i => i.Kind)
                );
                Assert.Equal(folder.FullPath, palette.Location);
            }
        }

        [Fact]
        public void LoadingADrawing_OpensTheTreeDownToIt_ListsItsKindsOfContent_AndPutsItInHistory()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });

                Assert.True(palette.Load(library));

                Assert.Equal("library.dxf", palette.Selected.Text);
                Assert.Equal(DesignCenterNodeKind.Drawing, palette.Selected.Kind);
                Assert.Equal(
                    new[]
                    {
                        "Blocks",
                        "Dimstyles",
                        "Layers",
                        "Layouts",
                        "Linetypes",
                        "Multileaderstyles",
                        "Tablestyles",
                        "Textstyles",
                        "Xrefs",
                    },
                    Names(palette.Items)
                );
                Assert.All(palette.Items, i => Assert.Equal(DesignCenterItemKind.Section, i.Kind));
                Assert.Equal("1 item", palette.Items[0].Description);
                Assert.Equal(new[] { library }, model.DesignCenterHistory);
                Assert.Equal(
                    new[]
                    {
                        "Blocks",
                        "Dimstyles",
                        "Layers",
                        "Layouts",
                        "Linetypes",
                        "Multileaderstyles",
                        "Tablestyles",
                        "Textstyles",
                        "Xrefs",
                    },
                    palette.Selected.Children.Select(c => c.Text)
                );
                Assert.False(palette.Load(Path.Combine(folder.FullPath, "missing.dxf")));
                Assert.StartsWith("Cannot find ", palette.Message);
            }
        }

        [Fact]
        public void ADoubleClickOnAKind_ShowsItsItems_WithTheirDescriptions_AndBackForwardAndUpWalkThePlacesVisited()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                palette.Load(library);

                palette.Activate(palette.Items.First(i => i.Name == "Layers"));
                Assert.Equal(new[] { "0", "ROAD", "WALL" }, Names(palette.Items));
                Assert.All(palette.Items, i => Assert.Equal(DesignCenterItemKind.Item, i.Kind));
                Assert.Equal(library + "\\Layers", palette.Location);

                palette.Activate(palette.Items.First(i => i.Name == "ROAD"));
                Assert.Equal("Layers", palette.Selected.Text);

                Assert.True(palette.UpCommand.CanExecute(null));
                palette.UpCommand.Execute(null);
                Assert.Equal("library.dxf", palette.Selected.Text);
                palette.UpCommand.Execute(null);
                Assert.Equal(folder.FullPath, palette.Selected.Path);

                palette.BackCommand.Execute(null);
                Assert.Equal("library.dxf", palette.Selected.Text);
                palette.BackCommand.Execute(null);
                Assert.Equal("Layers", palette.Selected.Text);
                Assert.Equal(new[] { "0", "ROAD", "WALL" }, Names(palette.Items));
                Assert.True(palette.ForwardCommand.CanExecute(null));
                palette.ForwardCommand.Execute(null);
                Assert.Equal("library.dxf", palette.Selected.Text);
            }
        }

        [Fact]
        public void ADoubleClickOnALayer_CopiesItIntoTheDrawingShown_AndALayerItHasIsKept()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                palette.NavigateTo(library, CadDesignCenterKind.Layers);

                palette.Activate(palette.Items.First(i => i.Name == "ROAD"));

                Assert.Equal("1 layer added.", palette.Message);
                Assert.Equal(3, model.Document.Database.LayerTable["ROAD"].Color.ColorIndex);
                Assert.Equal("GAS", model.Document.Database.LayerTable["ROAD"].LinetypeName);

                palette.Activate(palette.Items.First(i => i.Name == "WALL"));

                Assert.Equal("1 kept as the drawing has them.", palette.Message);
                Assert.Equal(5, model.Document.Database.LayerTable["WALL"].Color.ColorIndex);
                Assert.Equal("ADCENTER", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void TheMenuOfABlock_OffersInsertInsertWithThePaletteAndAddDefinition_AndInsertBlockPlacesItAtTheAskedPoint()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                palette.NavigateTo(library, CadDesignCenterKind.Blocks);
                DesignCenterItemViewModel tree = Assert.Single(palette.Items);
                Assert.Equal("Tree 3 m", tree.Description);
                Assert.NotNull(tree.Picture);

                IReadOnlyList<DesignCenterAction> actions = palette.ActionsFor(new[] { tree });
                Assert.Equal(
                    new[] { "Insert Block", "Insert Block…", "Add Block Definition" },
                    actions.Select(a => a.Text)
                );

                actions[0].Run();
                Assert.True(model.Session.IsRunning);
                model.Execute("100,50");

                DxfBlockReference placed = Assert.Single(
                    model.Document.Database.ModelSpace.OfType<DxfBlockReference>()
                );
                Assert.Equal("TREE", placed.Name);
                Assert.Equal(100.0, placed.Position.X, 6);
                Assert.Equal(50.0, placed.Position.Y, 6);
                Assert.True(model.Document.Database.BlockTable.Has("TREE"));
            }
        }

        [Fact]
        public void ABlockDroppedOnTheCanvas_IsPlacedWhereItIsDropped_ADrawingFileAsABlock_AndALayerCopiedIn()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                palette.NavigateTo(library, CadDesignCenterKind.Blocks);
                string dragged = palette.DragText(palette.Items);

                Assert.True(model.DropFromDesignCenter(dragged, new GeoPoint2(30, 40)));

                DxfBlockReference placed = Assert.Single(
                    model.Document.Database.ModelSpace.OfType<DxfBlockReference>()
                );
                Assert.Equal(30.0, placed.Position.X, 6);
                Assert.Equal(40.0, placed.Position.Y, 6);
                Assert.False(model.Session.IsRunning);

                Assert.True(
                    model.DropFromDesignCenter(
                        DesignCenterDrag.Write(library, null, new string[0]),
                        new GeoPoint2(0, 0)
                    )
                );
                Assert.True(model.Document.Database.BlockTable.Has("library"));

                Assert.True(
                    model.DropFromDesignCenter(
                        DesignCenterDrag.Write(library, CadDesignCenterKind.Layers, new[] { "ROAD", "WALL" }),
                        null
                    )
                );
                Assert.True(model.Document.Database.LayerTable.Has("ROAD"));
                Assert.Equal(5, model.Document.Database.LayerTable["WALL"].Color.ColorIndex);
                Assert.False(model.DropFromDesignCenter("not a drag", null));
            }
        }

        [Fact]
        public void TheOpenDrawingsTab_ListsTheDrawingsOpen_WithTheirContent_AndTheHistoryTabThoseShown()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });

                palette.Tab = "Open Drawings";
                DesignCenterNode plan = Assert.Single(palette.Roots);
                Assert.Equal("plan.dxf", plan.Text);
                plan.IsSelected = true;
                plan.IsExpanded = true;
                Assert.Equal(9, plan.Children.Count);
                palette.Activate(palette.Items.First(i => i.Name == "Layers"));
                Assert.Equal(new[] { "0", "WALL" }, Names(palette.Items));

                palette.Tab = "Folders";
                palette.Load(library);
                palette.Tab = "History";
                Assert.Equal(new[] { "library.dxf", "plan.dxf" }, palette.Roots.Select(r => r.Text));
                DesignCenterNode shown = palette.Roots[0];
                shown.IsSelected = true;
                Assert.Equal(9, palette.Items.Count);
                Assert.Equal("History", palette.Tab);
                palette.Tab = "Nowhere";
                Assert.Equal("History", palette.Tab);
            }
        }

        [Fact]
        public void ADrawingThatCannotBeRead_IsSaidInTheMessage_AndShowsNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                folder.WriteText("broken.dwg", "AC1015 and then nothing a reader can use");
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });

                Assert.True(palette.Load(Path.Combine(folder.FullPath, "broken.dwg")));

                Assert.StartsWith("Cannot read broken.dwg", palette.Message);
                Assert.Empty(palette.Items);
            }
        }

        [Fact]
        public void TheDragText_RoundTrips_AndAFolderOrAKindIsNotDragged()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palette = new DesignCenterViewModel(model, new[] { folder.FullPath });
                palette.Load(library);
                Assert.Null(palette.DragText(palette.Items));

                string text = DesignCenterDrag.Write(library, CadDesignCenterKind.Layers, new[] { "A", "B" });
                Assert.True(
                    DesignCenterDrag.TryRead(
                        text,
                        out string path,
                        out CadDesignCenterKind? kind,
                        out IReadOnlyList<string> names
                    )
                );
                Assert.Equal(library, path);
                Assert.Equal(CadDesignCenterKind.Layers, kind);
                Assert.Equal(new[] { "A", "B" }, names);

                Assert.True(
                    DesignCenterDrag.TryRead(
                        DesignCenterDrag.Write(library, null, new string[0]),
                        out _,
                        out CadDesignCenterKind? file,
                        out _
                    )
                );
                Assert.Null(file);
                Assert.False(DesignCenterDrag.TryRead("x\nNoSuchKind", out _, out _, out _));
                Assert.False(DesignCenterDrag.TryRead(string.Empty, out _, out _, out _));
            }
        }

        [Fact]
        public void Adcenter_ShowsThePalette_AdccloseClosesIt_AndTheKeyGivenAdcenterTogglesIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                DesignCenterViewModel shown = null;
                int closed = 0;
                model.ShowDesignCenter = palette =>
                {
                    shown = palette;
                    model.NoteDesignCenterShown(true);
                };
                model.CloseDesignCenter = () =>
                {
                    closed++;
                    model.NoteDesignCenterShown(false);
                };

                model.Execute("ADCENTER");
                Assert.NotNull(shown);
                Assert.True(model.IsDesignCenterShown);

                Assert.True(model.ToggleDesignCenter());
                Assert.Equal(1, closed);
                Assert.False(model.IsDesignCenterShown);
                Assert.True(model.ToggleDesignCenter());
                Assert.True(model.IsDesignCenterShown);

                model.Execute("ADCCLOSE");
                Assert.Equal(2, closed);

                model.Execute("DC");
                Assert.True(model.IsDesignCenterShown);
            }
        }
    }
}
