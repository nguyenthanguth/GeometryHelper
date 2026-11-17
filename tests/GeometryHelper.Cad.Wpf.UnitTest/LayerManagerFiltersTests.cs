using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Layer manager's Plot, Transparency, Plot Style and Description columns, and its filter tree as AutoCAD 2021's Layer
    /// Properties Manager has them: property filters made in Layer Filter Properties, group filters given layers, Invert filter,
    /// Apply to layers toolbar, the filter chosen kept in the drawing when the dialog closes. Driven through the view models;
    /// no window shown.
    /// </summary>
    public class LayerManagerFiltersTests
    {
        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        /// <summary>The room's drawing with named plot styles, Normal and Style 1.</summary>
        private static MainViewModel NamedRoom(TestFolder folder)
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            string text = db.ToText().Replace("\r\n", "\n");
            string dictionary = db.PlotStyleNameDictionary.Handle;
            string normal = "  3\nNormal\n350\n" + db.PlotStyleNameDictionary.GetAt("Normal").Handle + "\n";
            text = text.Replace(normal, normal + "  3\nStyle 1\n350\nFF01\n");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Insert(
                end,
                "  0\nACDBPLACEHOLDER\n  5\nFF01\n102\n{ACAD_REACTORS\n330\n"
                    + dictionary
                    + "\n102\n}\n330\n"
                    + dictionary
                    + "\n"
            );
            var model = new MainViewModel();
            Assert.True(model.Open(folder.WriteText("named.dxf", text)));
            return model;
        }

        private static LayerRowViewModel Row(LayerManagerViewModel manager, string name) =>
            manager.Rows.Single(r => r.Name == name);

        private static string[] Names(LayerManagerViewModel manager) => manager.Rows.Select(r => r.Name).ToArray();

        [Fact]
        public void ThePlotTransparencyAndDescriptionColumns_AreEditsOfTheLayer_EachOneUndoStep_AndRefusedAsMinusLayerRefusesThem()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                DxfLayerTableRecord walls = model.Document.Database.LayerTable["WALLS"];

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    LayerRowViewModel row = Row(manager, "WALLS");
                    Assert.True(row.IsPlotted);
                    Assert.Equal("0", row.Transparency);
                    Assert.Equal(string.Empty, row.Description);

                    row.IsPlotted = false;
                    Assert.False(walls.IsPlottable);
                    Assert.False(Row(manager, "WALLS").IsPlotted);

                    Row(manager, "WALLS").Transparency = "50";
                    Assert.Equal(new DxfTransparency(127), walls.Transparency);
                    Assert.Equal("50", Row(manager, "WALLS").Transparency);

                    Row(manager, "WALLS").Description = "Walls of the ground floor";
                    Assert.Equal("Walls of the ground floor", walls.Description);
                    Assert.Equal("Walls of the ground floor", Row(manager, "WALLS").Description);

                    int lines = model.History.Count;
                    Row(manager, "WALLS").Transparency = "95";
                    Assert.Equal("50", Row(manager, "WALLS").Transparency);
                    Row(manager, "WALLS").Transparency = "abc";
                    Assert.Equal("50", Row(manager, "WALLS").Transparency);
                    Assert.Equal(lines + 2, model.History.Count);
                    Assert.Equal("Invalid transparency \"abc\": give 0 to 90.", model.History.Last());
                    Assert.Contains("Transparency is from 0 to 90.", model.History);

                    // Three edits, each one undo step; the drawing is read back by an undo, so its layer is looked up again.
                    model.Undo();
                    Assert.Equal(string.Empty, model.Document.Database.LayerTable["WALLS"].Description);
                    model.Undo();
                    Assert.Equal(new DxfTransparency(255), model.Document.Database.LayerTable["WALLS"].Transparency);
                    model.Undo();
                    Assert.True(model.Document.Database.LayerTable["WALLS"].IsPlottable);
                }
            }
        }

        [Fact]
        public void ThePlotStyleColumn_IsGreyColor_N_InADrawingPlottedByColour_AndSetByNameInOneOfNamedPlotStyles()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel byColour = Room(folder);

                using (LayerManagerViewModel manager = byColour.CreateLayerManager())
                {
                    Assert.False(manager.HasNamedPlotStyles);
                    Assert.Equal("Color_1", Row(manager, "COLUMNS").PlotStyle);
                    Assert.Equal("Color_7", Row(manager, "WALLS").PlotStyle);
                    Assert.False(Row(manager, "COLUMNS").CanEditPlotStyle);

                    Row(manager, "COLUMNS").PlotStyle = "Normal";
                    Assert.Equal("Color_1", Row(manager, "COLUMNS").PlotStyle);
                }
            }

            using (var folder = new TestFolder())
            {
                MainViewModel named = NamedRoom(folder);

                using (LayerManagerViewModel manager = named.CreateLayerManager())
                {
                    Assert.True(manager.HasNamedPlotStyles);
                    Assert.Equal(new[] { "Normal", "Style 1" }, manager.PlotStyleNames);
                    Assert.Equal("Normal", Row(manager, "WALLS").PlotStyle);
                    Assert.True(Row(manager, "WALLS").CanEditPlotStyle);
                    Assert.Equal(new[] { "Normal", "Style 1" }, Row(manager, "WALLS").PlotStyleChoices);

                    Row(manager, "WALLS").PlotStyle = "Style 1";
                    Assert.Equal("Style 1", Row(manager, "WALLS").PlotStyle);
                    Assert.Equal("Style 1", named.Document.Database.LayerTable["WALLS"].PlotStyleName);
                    Assert.Equal("Normal", Row(manager, "COLUMNS").PlotStyle);

                    named.Undo();
                    Assert.Equal("Normal", named.Document.Database.LayerTable["WALLS"].PlotStyleName);
                }
            }
        }

        [Fact]
        public void TheFilterTree_IsAllAndTheThreeAutoCadMakes_AndAGroupFilterMadeIsNamedGroupFilter1AndOpenForARename()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    LayerFilterNodeViewModel all = manager.FilterTree.Single();
                    Assert.Equal("All", all.Name);
                    Assert.Same(all, manager.SelectedFilter);
                    Assert.Equal(
                        new[] { "All Used Layers", "Unreconciled New Layers", "Viewport Overrides" },
                        all.Children.Select(c => c.Name)
                    );
                    Assert.Equal("All: displaying 3 of 3 layers", manager.FilterStatus);

                    Assert.Equal("Group Filter1", manager.NewGroupFilter());
                    LayerFilterNodeViewModel group = manager.SelectedFilter;
                    Assert.Equal("Group Filter1", group.Name);
                    Assert.True(group.IsGroup);
                    Assert.True(group.IsRenaming);
                    Assert.Empty(manager.Rows);
                    Assert.Equal("Group Filter1: displaying 0 of 3 layers", manager.FilterStatus);
                    // Made while a filter of the drawing is chosen, it is nested in it; while All is chosen, at the top.
                    Assert.Equal("Group Filter1", manager.NewGroupFilter());
                    Assert.Equal(new[] { "Group Filter1", "Group Filter1" }, manager.SelectedFilter.Path);
                    manager.SelectedFilter = manager.FilterTree.Single();
                    Assert.Equal("Group Filter2", manager.NewGroupFilter());
                    Assert.Equal(new[] { "Group Filter2" }, manager.SelectedFilter.Path);
                }
            }
        }

        [Fact]
        public void LayersAddedToAGroupFilter_AreThoseTheListShowsWhenItIsChosen_ByDragSelectLayersOrAddToGroupFilter()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    manager.NewGroupFilter();
                    LayerFilterNodeViewModel group = manager.SelectedFilter;
                    manager.CancelRenameFilter(group);

                    // A layer dragged onto the group, and the chosen layers added from the right-click item.
                    manager.SelectedFilter = manager.FilterTree.Single();
                    Assert.True(manager.AddLayersToGroup(manager.Find(new[] { "Group Filter1" }), new[] { "WALLS" }));
                    Row(manager, "COLUMNS").IsChosen = true;
                    manager.AddChosenToGroupCommand.Execute(manager.Find(new[] { "Group Filter1" }));
                    manager.SelectedFilter = manager.Find(new[] { "Group Filter1" });
                    Assert.Equal(new[] { "COLUMNS", "WALLS" }, Names(manager));
                    Assert.Equal("Group Filter1: displaying 2 of 3 layers", manager.FilterStatus);

                    // Select Layers, Replace: the layers of the objects selected in the drawing (the circle is on COLUMNS).
                    model.Document.Selection.Add(
                        new[] { model.Document.Database.ModelSpace.OfType<DxfCircle>().Single().Handle }
                    );
                    Assert.True(manager.SelectLayers(manager.SelectedFilter, true));
                    Assert.Equal(new[] { "COLUMNS" }, Names(manager));
                    Assert.True(manager.SelectLayers(manager.SelectedFilter, false));
                    Assert.Equal(new[] { "COLUMNS" }, Names(manager));

                    // A property filter takes no layers by drag.
                    Assert.False(manager.AddLayersToGroup(manager.FilterTree.Single(), new[] { "WALLS" }));
                    Assert.Contains("Layers are added to a group filter: choose one.", model.History);
                }
            }
        }

        [Fact]
        public void APropertyFilterMadeInLayerFilterProperties_ListsTheLayersItLetsThrough_AndInvertFilterTheOthers()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    LayerFilterEditViewModel seen = null;
                    manager.EditFilter = dialog =>
                    {
                        seen = dialog;
                        Assert.Equal("Properties Filter1", dialog.Name);
                        Assert.Equal(3, dialog.Preview.Count);
                        dialog.Name = "Walls";
                        dialog.Rows[0].Name = "wall*";
                        Assert.Equal("NAME==\"wall*\"", dialog.Expression);
                        Assert.Equal(new[] { "WALLS" }, dialog.Preview.Select(p => p.Name));
                        return true;
                    };

                    Assert.Equal("Walls", manager.NewPropertyFilter());
                    Assert.NotNull(seen);
                    LayerFilterNodeViewModel walls = manager.Find(new[] { "Walls" });
                    Assert.Equal(LayerFilterNodeKind.Property, walls.Kind);
                    Assert.Equal("NAME==\"wall*\"", walls.Filter.Expression);

                    manager.SelectedFilter = walls;
                    Assert.Equal(new[] { "WALLS" }, Names(manager));
                    Assert.Equal("Walls: displaying 1 of 3 layers", manager.FilterStatus);

                    manager.InvertFilter = true;
                    Assert.Equal(new[] { "0", "COLUMNS" }, Names(manager));
                    Assert.Equal("Walls (inverted): displaying 2 of 3 layers", manager.FilterStatus);
                    manager.InvertFilter = false;

                    // A filter nested in it shows the layers both let through.
                    manager.EditFilter = dialog =>
                    {
                        Assert.Equal("Properties Filter1", dialog.Name);
                        dialog.Rows[0].Color = "white";
                        return true;
                    };
                    Assert.Equal("Properties Filter1", manager.NewPropertyFilter());
                    manager.SelectedFilter = manager.Find(new[] { "Walls", "Properties Filter1" });
                    Assert.Equal(new[] { "WALLS" }, Names(manager));
                    Assert.Equal("COLOR==\"7\"", manager.SelectedFilter.Filter.Expression);

                    // Cancel makes nothing.
                    manager.EditFilter = dialog => false;
                    Assert.Null(manager.NewPropertyFilter());
                    Assert.Equal(2, manager.Find(new[] { "Walls" }).Children.Count + 1);
                }
            }
        }

        [Fact]
        public void APropertyFilterIsChangedAndRenamedByItsProperties_ADefinitionTheRowsCannotShow_IsKeptAsTextAndEdited()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLayerFilters.AddProperty(model.Document, null, "Dim", "NAME==\"*dim\" OR COLOR==\"1\"");
                CadLayerFilters.AddProperty(model.Document, null, "Twice", "NAME==\"A*\" AND NAME==\"*B\"");

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    manager.EditFilter = dialog =>
                    {
                        Assert.False(dialog.IsRaw);
                        Assert.Equal(2, dialog.Rows.Count);
                        Assert.Equal("*dim", dialog.Rows[0].Name);
                        Assert.Equal("1", dialog.Rows[1].Color);
                        Assert.Equal(new[] { "COLUMNS" }, dialog.Preview.Select(p => p.Name));
                        dialog.Rows[1].Color = "Blue";
                        dialog.Name = "Dimensions";
                        return true;
                    };
                    Assert.True(manager.ChangeFilter(manager.Find(new[] { "Dim" })));
                    Assert.Null(manager.Find(new[] { "Dim" }));
                    Assert.Equal(
                        "NAME==\"*dim\" OR COLOR==\"5\"",
                        manager.Find(new[] { "Dimensions" }).Filter.Expression
                    );

                    manager.EditFilter = dialog =>
                    {
                        Assert.True(dialog.IsRaw);
                        Assert.Equal("NAME==\"A*\" AND NAME==\"*B\"", dialog.Expression);
                        dialog.Expression = "NAME==\"A*\" AND NAME==\"*C\"";
                        return true;
                    };
                    Assert.True(manager.ChangeFilter(manager.Find(new[] { "Twice" })));
                    Assert.Equal("NAME==\"A*\" AND NAME==\"*C\"", manager.Find(new[] { "Twice" }).Filter.Expression);

                    // Renaming and changing it is one undo step.
                    model.Undo();
                    Assert.Equal("NAME==\"A*\" AND NAME==\"*B\"", manager.Find(new[] { "Twice" }).Filter.Expression);
                }
            }
        }

        [Fact]
        public void TheLayerFilterPropertiesDialog_MakesTheExpressionFromItsRows_AndRefusesANameTakenOrAnEmptyDefinition()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLayerFilters.AddProperty(model.Document, null, "Steel", "NAME==\"S*\"");
                var dialog = new LayerFilterEditViewModel(model.Document, null, null, null);

                Assert.Equal("Properties Filter1", dialog.Name);
                Assert.False(dialog.CanOk);
                Assert.Equal("The filter definition is empty: give at least one property a value.", dialog.Message);

                LayerFilterRuleRowViewModel row = dialog.Rows[0];
                row.Name = "A*";
                row.On = "On";
                row.Freeze = "Thaw";
                row.Lock = "Unlock";
                row.Plot = "No plot";
                row.NewVpFreeze = "Frozen";
                row.VpFreeze = "Thawed";
                row.Transparency = "50";
                row.Lineweight = "0.25";
                row.Linetype = "Continuous";
                row.PlotStyle = "Normal";
                row.Description = "x*";
                row.Color = "Red";
                dialog.AddRow();
                dialog.Rows[1].Name = "B*";
                Assert.Equal(
                    "NAME==\"A*\" AND OFF==\"False\" AND FROZEN==\"False\" AND LOCKED==\"False\" AND COLOR==\"1\" AND LINETYPE==\"Continuous\" AND LINEWEIGHT==\"0.25\" AND TRANSPARENCY==\"50\" AND PLOTSTYLE==\"Normal\" AND PLOTTABLE==\"False\" AND NEWVPFROZEN==\"True\" AND DESCRIPTION==\"x*\" AND VPFROZEN==\"False\" OR NAME==\"B*\"",
                    dialog.Expression
                );
                Assert.True(dialog.CanOk);

                dialog.Name = "steel";
                Assert.False(dialog.CanOk);
                Assert.Equal("A layer filter named \"steel\" already exists.", dialog.Message);
                Assert.False(dialog.Apply().Succeeded);

                dialog.Name = "All Used Layers";
                Assert.False(dialog.CanOk);

                dialog.RemoveRow(dialog.Rows[0]);
                dialog.Name = "Beams";
                Assert.Equal("NAME==\"B*\"", dialog.Expression);
                Assert.True(dialog.Apply().Succeeded);
                Assert.Equal(
                    new[] { "Beams", "Steel" },
                    CadLayerFilters.Read(model.Document.Database).Select(f => f.Name)
                );
            }
        }

        [Fact]
        public void ApplyToLayersToolbar_MakesTheRibbonsLayerBoxListOnlyTheLayersOfTheFilter_TheCurrentLayerAlways()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLayerFilters.AddProperty(model.Document, null, "Columns", "NAME==\"COL*\"");

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    manager.SelectedFilter = manager.Find(new[] { "Columns" });
                    Assert.Equal(3, model.Layers.Count);

                    manager.ApplyFilterToToolbar = true;
                    Assert.Equal(new[] { "COLUMNS", "WALLS" }, model.Layers.Select(l => l.Name));
                    Assert.Equal("WALLS", model.CurrentLayer.Name);

                    manager.InvertFilter = true;
                    Assert.Equal(new[] { "0", "WALLS" }, model.Layers.Select(l => l.Name));

                    manager.ApplyFilterToToolbar = false;
                    Assert.Equal(3, model.Layers.Count);
                }
            }
        }

        [Fact]
        public void TheFilterChosen_IsKeptInTheDrawingAsTheCurrentOne_WhenTheDialogCloses_OneUndoStepWhereItChanged()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLayerFilters.AddProperty(model.Document, null, "Columns", "NAME==\"COL*\"");

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    manager.SelectedFilter = manager.Find(new[] { "Columns" });
                    Assert.False(CadLayerFilters.Read(model.Document.Database).Single().IsCurrent);
                }

                Assert.True(CadLayerFilters.Read(model.Document.Database).Single().IsCurrent);
                Assert.Equal("LAYER", model.Document.NextUndo.Name);

                // Opened again, the dialog lists the layers of the current filter; closed with All chosen, none is current.
                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.Equal("Columns", manager.SelectedFilter.Name);
                    Assert.Equal(new[] { "COLUMNS" }, Names(manager));
                    manager.SelectedFilter = manager.FilterTree.Single();
                }

                Assert.False(CadLayerFilters.Read(model.Document.Database).Single().IsCurrent);

                // Closed with the filter as it was: no step.
                CadUndoStep before = model.Document.NextUndo;
                using (LayerManagerViewModel manager = model.CreateLayerManager()) { }
                Assert.Same(before, model.Document.NextUndo);
            }
        }

        [Fact]
        public void AFilterRenamedInTheTree_KeepsItsPlace_ADeletedOneFallsBackToAll_AndAConvertedOneBecomesAGroup()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLayerFilters.AddProperty(model.Document, null, "Columns", "NAME==\"COL*\"");
                CadLayerFilters.AddProperty(model.Document, null, "Other", "NAME==\"WALLS\"");

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    LayerFilterNodeViewModel columns = manager.Find(new[] { "Columns" });
                    manager.SelectedFilter = columns;
                    Assert.True(manager.BeginRenameFilter(columns));
                    columns.EditName = "other";
                    Assert.False(manager.CommitRenameFilter(columns));
                    Assert.True(columns.IsRenaming);
                    Assert.Equal("A layer filter named \"other\" already exists.", model.History.Last());

                    columns.EditName = " Round ";
                    Assert.True(manager.CommitRenameFilter(columns));
                    Assert.Equal("Round", manager.SelectedFilter.Name);
                    Assert.Equal(new[] { "COLUMNS" }, Names(manager));

                    Assert.True(manager.ConvertToGroupCommand.CanExecute(null));
                    manager.ConvertToGroupCommand.Execute(null);
                    Assert.Equal(LayerFilterNodeKind.Group, manager.Find(new[] { "Round" }).Kind);
                    manager.SelectedFilter = manager.Find(new[] { "Round" });
                    Assert.False(manager.ConvertToGroupCommand.CanExecute(null));

                    Assert.True(manager.DeleteFilter(manager.SelectedFilter));
                    Assert.Equal("All", manager.SelectedFilter.Name);
                    Assert.Equal(3, manager.Rows.Count);
                    Assert.Null(manager.Find(new[] { "Round" }));

                    // The filters AutoCAD makes are not renamed or deleted.
                    manager.SelectedFilter = manager.Find(new[] { "All Used Layers" });
                    Assert.False(manager.DeleteFilterCommand.CanExecute(null));
                    Assert.False(manager.RenameFilterCommand.CanExecute(null));
                    Assert.Equal(new[] { "0", "COLUMNS", "WALLS" }, Names(manager));
                }
            }
        }

        [Fact]
        public void TheLayerManagerWindow_ShowsTheFilterTreeWithEveryNodeADropTarget_AndTheNewColumnsInEveryRow()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Room(folder);
                        CadLayerFilters.AddGroup(model.Document, null, "Outer", new[] { "WALLS" });
                        CadLayerFilters.AddGroup(model.Document, new[] { "Outer" }, "Inner", new[] { "WALLS" });

                        using (LayerManagerViewModel manager = model.CreateLayerManager())
                        {
                            var window = new LayerManagerWindow(manager);
                            FrameworkElement body = window.Body;
                            object context = window.DataContext;
                            window.Content = null;
                            body.DataContext = context;
                            body.Measure(new Size(1380, 600));
                            body.Arrange(new Rect(0, 0, 1380, 600));
                            body.UpdateLayout();

                            TreeView tree = Descendants(body).OfType<TreeView>().Single();
                            List<TreeViewItem> items = Descendants(tree).OfType<TreeViewItem>().ToList();
                            Assert.Contains(items, i => ((LayerFilterNodeViewModel)i.DataContext).Name == "Inner");
                            Assert.All(items, i => Assert.True(i.AllowDrop));

                            // The headers of the columns after Weight, and a cell of each in every row.
                            List<string> heads = Descendants(body).OfType<TextBlock>().Select(t => t.Text).ToList();
                            foreach (string head in new[] { "Transparency", "Plot Style", "Plot", "Description" })
                            {
                                Assert.Contains(head, heads);
                            }

                            Assert.Equal(
                                manager.Rows.Count,
                                Descendants(body)
                                    .OfType<ComboBox>()
                                    .Count(c =>
                                        "Transparency".Equals(System.Windows.Automation.AutomationProperties.GetName(c))
                                    )
                            );
                            Assert.Equal(
                                manager.Rows.Count,
                                Descendants(body)
                                    .OfType<TextBox>()
                                    .Count(c =>
                                        "Description".Equals(System.Windows.Automation.AutomationProperties.GetName(c))
                                    )
                            );
                            window.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
