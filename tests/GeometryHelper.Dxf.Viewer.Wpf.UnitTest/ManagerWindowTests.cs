using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The managers of round six driven with no window: the Block Attribute Manager with its Edit Attribute and Settings, Edit
    /// Attributes on INSERT with ATTDIA 1, Purge in both its modes, Object Grouping with its Order Group; and each of their
    /// windows made over its view model, as the screenshot mode draws them.
    /// </summary>
    public class ManagerWindowTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>
        /// A drawing of R2013: the block GB with a circle and the attributes GRID (Grid letter, A), LEVEL (Level name, L1) and
        /// a second GRID (Grid again, B), one INSERT of it in model space; the block TB with eleven attributes T1 to T11 and
        /// the constant REV; the block OLD inserted nowhere; the layers TMP (empty) and WALLS (two lines); two lines grouped
        /// as PAIR.
        /// </summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            var gb = new DxfBlockTableRecord { Name = "GB" };
            db.BlockTable.Add(gb);
            gb.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 5));
            gb.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "GRID", "Grid letter", null) { Height = 2.5 }
            );
            gb.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, -4, 0), "L1", "LEVEL", "Level name", null) { Height = 2.5 }
            );
            gb.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, -8, 0), "B", "GRID", "Grid again", null) { Height = 2.5 }
            );
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), gb);
            db.ModelSpace.AppendEntity(insert);

            foreach (DxfAttributeDefinition definition in gb.OfType<DxfAttributeDefinition>())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            var tb = new DxfBlockTableRecord { Name = "TB" };
            db.BlockTable.Add(tb);

            for (int i = 1; i <= 11; i++)
            {
                tb.AppendEntity(
                    new DxfAttributeDefinition(new GeoPoint3(0, -5 * i, 0), "V" + i, "T" + i, "Field " + i, null)
                    {
                        Height = 2.5,
                    }
                );
            }

            tb.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, -70, 0), "0", "REV", "Revision", null)
                {
                    Height = 2.5,
                    Constant = true,
                }
            );
            var old = new DxfBlockTableRecord { Name = "OLD" };
            db.BlockTable.Add(old);
            old.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "TMP" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(100, 20, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 30, 0), new GeoPoint3(100, 30, 0)) { Layer = "WALLS" }
            );
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan-level2.dxf", Drawing())));
            List<string> walls = model
                .Document.ModelSpace.Block.Where(e => e.Layer == "WALLS")
                .Select(e => e.Handle)
                .ToList();
            model.Execute("-GROUP");
            model.Execute(string.Empty);
            model.Execute("PAIR");
            model.Execute("Pair of walls");
            model.Session.FeedSelection(walls);
            model.Execute(string.Empty);
            return model;
        }

        private static List<DxfAttributeDefinition> Definitions(MainViewModel model, string block) =>
            model.Document.Database.BlockTable[block].OfType<DxfAttributeDefinition>().ToList();

        [Fact]
        public void TheManagerGreysMoveUpOnTheFirstRow_DrawsTagsGivenTwiceInRed_AndEditThenSyncReachTheBlockAndItsInserts()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool reached = false;
                model.ShowBlockAttributeManager = manager =>
                {
                    Assert.Equal(new[] { "GB", "TB" }, manager.Blocks);
                    Assert.Equal("GB", manager.Block);
                    Assert.Equal("1", manager.FoundInDrawing);
                    Assert.Equal(new[] { "Tag", "Prompt", "Default", "Modes" }, manager.Columns.Select(c => c.Name));
                    Assert.Equal(new[] { true, false, true }, manager.Rows.Select(r => r.IsDuplicate));
                    Assert.Same(manager.Rows[0], manager.Selected);
                    Assert.False(manager.CanMoveUp);
                    Assert.True(manager.CanMoveDown);

                    manager.MoveDown();
                    Assert.Equal(new[] { "LEVEL", "GRID", "GRID" }, manager.Rows.Select(r => r.Tag));
                    Assert.True(manager.CanMoveUp);

                    manager.ShowEditor = editor =>
                    {
                        Assert.Equal("GB", editor.Block);
                        Assert.Equal("GRID", editor.Tag);
                        editor.Tag = "axis";
                        editor.Height = "0";
                        Assert.False(editor.Ok());
                        Assert.Equal("Value must be positive and nonzero.", editor.Message);
                        editor.Height = "3.5";
                        Assert.True(editor.Ok());
                        return true;
                    };

                    Assert.True(manager.Edit());
                    Assert.Equal(new[] { false, false, false }, manager.Rows.Select(r => r.IsDuplicate));
                    Assert.Equal("AXIS", manager.Rows[1].Tag);
                    manager.Sync();
                    Assert.Equal(new[] { "LEVEL", "AXIS", "GRID" }, manager.Rows.Select(r => r.Tag));

                    manager.Block = "TB";
                    Assert.Equal(12, manager.Rows.Count);
                    Assert.Equal("0", manager.FoundInDrawing);
                    reached = true;
                    manager.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("BATTMAN");

                Assert.True(reached);
                Assert.False(model.Session.IsRunning);
                Assert.Equal(new[] { "LEVEL", "AXIS", "GRID" }, Definitions(model, "GB").Select(d => d.Tag));
                Assert.Equal(3.5, Definitions(model, "GB")[1].Height, 9);
                DxfBlockReference insert = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single();
                Assert.Equal(new[] { "LEVEL", "AXIS", "GRID" }, insert.AttributeCollection.Select(a => a.Tag));
                Assert.Equal("BATTMAN", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void TheManagersSettings_KeepTagShown_AddTheColumnsChecked_AndRemoveIsGreyedWithOneAttributeLeft()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool reached = false;
                model.ShowBlockAttributeManager = manager =>
                {
                    manager.ShowSettings = settings =>
                    {
                        AttributeSettingColumnViewModel tag = settings.Columns.Single(c => c.Name == "Tag");
                        Assert.False(tag.IsEnabled);
                        settings.ClearAll();
                        Assert.True(tag.IsChecked);
                        Assert.Equal(new[] { "Tag" }, settings.Columns.Where(c => c.IsChecked).Select(c => c.Name));
                        settings.SelectAll();
                        Assert.False(settings.Columns.Single(c => c.Name == "Plot style").IsChecked);
                        settings.ClearAll();
                        settings.Columns.Single(c => c.Name == "Layer").IsChecked = true;
                        settings.Columns.Single(c => c.Name == "Height").IsChecked = true;
                        settings.EmphasizeDuplicateTags = false;
                        return true;
                    };

                    Assert.True(manager.OpenSettings());
                    Assert.Equal(new[] { "Tag", "Height", "Layer" }, manager.Columns.Select(c => c.Name));
                    Assert.Equal(new[] { "GRID", "2.5", "0" }, manager.Rows[0].Cells.Select(c => c.Text));
                    Assert.All(manager.Rows, r => Assert.False(r.IsDuplicate));

                    manager.Remove();
                    manager.Remove();
                    Assert.Single(manager.Rows);
                    Assert.False(manager.CanRemove);
                    reached = true;
                    manager.Close(CadDialogAction.Cancel);
                    return true;
                };

                model.Execute("BATTMAN");

                Assert.True(reached);
                Assert.Equal(3, Definitions(model, "GB").Count);
                Assert.Equal(new[] { "Tag", "Height", "Layer" }, model.Session.AttributeManager.Columns);
            }
        }

        [Fact]
        public void EditAttributesOnInsert_ShowsEightAttributesAPage_LeavesTheConstantOut_AndOkGivesTheValues()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool reached = false;
                model.ShowEditAttributes = dialog =>
                {
                    Assert.Equal("TB", dialog.Block);
                    Assert.Equal(2, dialog.Pages);
                    Assert.Equal("Page 1 of 2", dialog.PageText);
                    Assert.Equal(8, dialog.Rows.Count);
                    Assert.False(dialog.CanPrevious);
                    Assert.Equal("Field 1", dialog.Rows[0].Prompt);
                    dialog.Rows[0].Value = "LEVEL 2 PLAN";
                    dialog.Page++;
                    Assert.Equal(new[] { "Field 9", "Field 10", "Field 11" }, dialog.Rows.Select(r => r.Prompt));
                    Assert.False(dialog.CanNext);
                    dialog.Rows[2].Value = "Riverside block B";
                    reached = true;
                    dialog.Close(CadDialogAction.Ok);
                    return true;
                };

                foreach (
                    string line in new[]
                    {
                        "ATTDIA",
                        "1",
                        "-INSERT",
                        "TB",
                        "500,0",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                    }
                )
                {
                    model.Execute(line);
                }

                Assert.True(reached);
                Assert.False(model.Session.IsRunning);
                DxfBlockReference placed = model
                    .Document.ModelSpace.Block.OfType<DxfBlockReference>()
                    .Single(r => r.Name == "TB");
                Assert.Equal(11, placed.AttributeCollection.Count);
                Assert.Equal("LEVEL 2 PLAN", placed.AttributeCollection[0].TextString);
                Assert.Equal("V2", placed.AttributeCollection[1].TextString);
                Assert.Equal("Riverside block B", placed.AttributeCollection[10].TextString);
            }
        }

        [Fact]
        public void Purge_ListsWhatNothingUsesByKind_PurgesTheCheckedOnesAsked_AndShowsWhyALayerInUseCannotGo()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var asked = new List<string>();
                bool reached = false;
                model.ConfirmPurgeItem = question =>
                {
                    asked.Add(question);
                    return true;
                };
                model.ShowPurge = purge =>
                {
                    Assert.Equal("Purge - plan-level2.dxf", purge.Title);
                    Assert.Equal("All Items", purge.Nodes[0].Name);
                    PurgeNodeViewModel blocks = purge.Nodes.Single(n => n.Name == "Blocks");
                    Assert.Equal("(2)", blocks.Count);
                    Assert.Equal(new[] { "OLD", "TB" }, blocks.Children.Select(c => c.Name));
                    Assert.True(blocks.IsExpanded);
                    Assert.False(purge.Nodes.Single(n => n.Name == "Shapes").IsEnabled);
                    Assert.Same(blocks.Children[0], purge.Selected);
                    Assert.NotNull(purge.Preview);
                    Assert.Equal("Block OLD · not used anywhere.", purge.PreviewNote);

                    // Only OLD and the layer TMP checked.
                    purge.Nodes[0].IsChecked = false;
                    blocks.Children[0].IsChecked = true;
                    purge.Nodes.Single(n => n.Name == "TMP").IsChecked = true;
                    Assert.Equal(2, purge.PurgeChecked());
                    Assert.Equal(
                        new[]
                        {
                            "You are about to purge block OLD. What do you want to do?",
                            "You are about to purge layer TMP. What do you want to do?",
                        },
                        asked
                    );
                    Assert.Equal(
                        new[] { "TB" },
                        purge.Nodes.Single(n => n.Name == "Blocks").Children.Select(c => c.Name)
                    );

                    purge.Mode = purge.Modes[1];
                    Assert.Equal("Named items in use", purge.TreeTitle);
                    PurgeNodeViewModel layers = purge.Nodes.Single(n => n.Name == "Layers");
                    Assert.False(layers.IsExpanded);
                    layers.IsExpanded = true;
                    purge.Selected = purge.Nodes.Single(n => n.Name == "WALLS");
                    Assert.Equal("Layer WALLS", purge.ReasonsTitle);
                    Assert.Equal(new[] { "Used by 2 objects in model space and 0 in layouts." }, purge.Reasons);
                    Assert.True(purge.CanSelectObjects);
                    purge.SelectObjects();
                    reached = true;
                    return true;
                };

                model.Execute("PURGE");

                Assert.True(reached);
                Assert.False(model.Document.Database.BlockTable.Has("OLD"));
                Assert.False(model.Document.Database.LayerTable.Has("TMP"));
                Assert.Equal(2, model.Document.Selection.Handles.Count);
            }
        }

        [Fact]
        public void ChoosingAnotherBlockWithChangesMade_AsksToSaveThem_NoDropsThem_YesWritesThem_AndAConstantDoesNotMove()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var asked = new List<string>();
                bool answer = false;
                bool reached = false;
                model.ShowBlockAttributeManager = manager =>
                {
                    manager.AskSaveChanges = block =>
                    {
                        asked.Add(block);
                        return answer;
                    };

                    // Nothing changed in GB: TB is shown without asking.
                    manager.Block = "TB";
                    Assert.Empty(asked);

                    manager.Selected = manager.Rows.Last();
                    Assert.Equal("REV", manager.Selected.Tag);
                    Assert.False(manager.CanMoveUp);
                    Assert.False(manager.CanMoveDown);
                    manager.MoveUp();
                    Assert.Equal("REV", manager.Rows.Last().Tag);

                    manager.Selected = manager.Rows[0];
                    manager.MoveDown();
                    Assert.True(manager.Request.HasChanges);
                    manager.Block = "GB";
                    Assert.Equal(new[] { "TB" }, asked);

                    manager.MoveDown();
                    answer = true;
                    manager.Block = "TB";
                    Assert.Equal(new[] { "TB", "GB" }, asked);
                    Assert.Equal("T1", manager.Rows[0].Tag);
                    Assert.False(manager.Request.HasChanges);
                    reached = true;
                    manager.Close(CadDialogAction.Cancel);
                    return true;
                };

                model.Execute("BATTMAN");

                Assert.True(reached);
                Assert.Equal(new[] { "LEVEL", "GRID", "GRID" }, Definitions(model, "GB").Select(d => d.Tag));
                Assert.Equal("T1", Definitions(model, "TB")[0].Tag);
            }
        }

        [Fact]
        public void SelectBlockWithChangesMade_AsksToSaveThemFirst_AndNoLeavesTheBlockAsItWas()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string insert = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
                var asked = new List<string>();
                int shown = 0;
                model.ShowBlockAttributeManager = manager =>
                {
                    shown++;
                    manager.AskSaveChanges = block =>
                    {
                        asked.Add(block);
                        return false;
                    };

                    if (shown == 1)
                    {
                        manager.MoveDown();
                        manager.Close(CadDialogAction.SelectBlock);
                        return true;
                    }

                    Assert.Equal(new[] { "GRID", "LEVEL", "GRID" }, manager.Rows.Select(r => r.Tag));
                    manager.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("BATTMAN");
                Assert.Equal("Select a block:", model.Session.PromptText);
                model.Session.FeedSelection(new[] { insert });

                Assert.Equal(2, shown);
                Assert.Equal(new[] { "GB" }, asked);
                Assert.False(model.Session.IsRunning);
                Assert.Equal(new[] { "GRID", "LEVEL", "GRID" }, Definitions(model, "GB").Select(d => d.Tag));
            }
        }

        [Fact]
        public void Purge_PreviewsAMultileaderStyleAndATableStyleNothingUses_AsTheirDialogsDrawThem()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                Assert.True(CadTableStyleEditor.Add(model.Document, "SCHEDULE", null).Succeeded);
                Assert.True(CadMLeaderStyleEditor.Add(model.Document, "NOTES", null).Succeeded);
                bool reached = false;
                model.ShowPurge = purge =>
                {
                    Assert.Null(purge.TablePreview);

                    purge.Selected = purge.Nodes.Single(n => n.Name == "NOTES");
                    Assert.NotNull(purge.Preview);
                    Assert.Null(purge.TablePreview);
                    Assert.Equal("Multileader style NOTES · not used anywhere.", purge.PreviewNote);

                    purge.Selected = purge.Nodes.Single(n => n.Name == "SCHEDULE");
                    Assert.Null(purge.Preview);
                    Assert.NotNull(purge.TablePreview);
                    Assert.Equal("Table style SCHEDULE · not used anywhere.", purge.PreviewNote);

                    purge.Mode = purge.Modes[1];
                    Assert.Null(purge.TablePreview);
                    reached = true;
                    return true;
                };

                model.Execute("PURGE");

                Assert.True(reached);
            }
        }

        [Fact]
        public void ObjectGrouping_FillsTheBoxesFromTheGroupChosen_RenamesIt_AndNewAsksOnTheCommandLineThenComesBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string circle = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
                int shown = 0;
                model.ShowGroupManager = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        GroupRowViewModel pair = Assert.Single(dialog.Groups);
                        Assert.Equal("Yes", pair.Selectable);
                        Assert.False(dialog.HasSelected);
                        dialog.Selected = pair;
                        Assert.Equal("PAIR", dialog.GroupName);
                        Assert.Equal("Pair of walls", dialog.Description);

                        dialog.GroupName = "WALLS_EAST";
                        Assert.True(dialog.Rename());
                        Assert.Equal("WALLS_EAST", dialog.Selected.Name);
                        dialog.ShowOrder = order =>
                        {
                            Assert.Equal("Positions 0 to 1", order.Positions);
                            order.RemoveFrom = "0";
                            order.NewPosition = "1";
                            Assert.True(order.Reorder());
                            order.Count = "x";
                            Assert.False(order.Reorder());
                            Assert.Equal("Requires a whole number.", order.Message);
                            return false;
                        };
                        Assert.False(dialog.Reorder());

                        dialog.GroupName = "BUBBLE";
                        dialog.NewSelectable = false;
                        dialog.Pick("New");
                        return true;
                    }

                    Assert.Equal(new[] { "BUBBLE", "WALLS_EAST" }, dialog.Groups.Select(g => g.Name));
                    Assert.Equal("BUBBLE", dialog.Selected.Name);
                    Assert.Equal("No", dialog.Selected.Selectable);
                    dialog.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("CLASSICGROUP");
                Assert.Equal("Select objects for grouping:", model.Session.PromptText);
                model.Session.FeedSelection(new[] { circle });
                model.Execute(string.Empty);

                Assert.Equal(2, shown);
                Assert.False(model.Session.IsRunning);
                Assert.True(model.Document.Database.GroupDictionary.Contains("BUBBLE"));
                Assert.True(model.Document.Database.GroupDictionary.Contains("WALLS_EAST"));
                Assert.Equal("CLASSICGROUP", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void EveryManagerWindow_IsMadeOverItsViewModel_AsTheScreenshotModeDrawsIt()
        {
            using (var folder = new TestFolder())
            {
                string path = folder.Write("plan-level2.dxf", Drawing());
                Exception failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(path));
                        object shown = null;
                        model.ShowBlockAttributeManager = d =>
                        {
                            shown = d;
                            return true;
                        };
                        model.Execute("BATTMAN");
                        var manager = (BlockAttributeManagerViewModel)shown;
                        var windows = new List<Window>
                        {
                            new BlockAttributeManagerWindow(manager),
                            new EditAttributeWindow(
                                new EditAttributeViewModel(model.Document, manager.Block, manager.Request.Rows[0])
                            ),
                            new AttributeManagerSettingsWindow(
                                new AttributeManagerSettingsViewModel(manager.Request.Settings)
                            ),
                        };
                        model.ShowPurge = d =>
                        {
                            shown = d;
                            return true;
                        };
                        model.Execute("PURGE");
                        windows.Add(new PurgeWindow((PurgeViewModel)shown));
                        model.ShowGroupManager = d =>
                        {
                            shown = d;
                            return true;
                        };
                        model.Execute("CLASSICGROUP");
                        windows.Add(new GroupManagerWindow((GroupManagerViewModel)shown));
                        windows.Add(
                            new OrderGroupWindow(new OrderGroupViewModel(((GroupManagerViewModel)shown).Request))
                        );
                        model.ShowEditAttributes = d =>
                        {
                            shown = d;
                            return true;
                        };

                        foreach (
                            string line in new[]
                            {
                                "ATTDIA",
                                "1",
                                "-INSERT",
                                "TB",
                                "500,0",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                            }
                        )
                        {
                            model.Execute(line);
                        }

                        windows.Add(new EditAttributesWindow((EditAttributesViewModel)shown));

                        foreach (Window window in windows)
                        {
                            var body = (FrameworkElement)window.Content;
                            window.Content = null;
                            body.DataContext = window.DataContext;
                            body.Measure(new Size(window.Width, window.Height));
                            body.Arrange(new Rect(0, 0, window.Width, window.Height));
                            body.UpdateLayout();
                            Assert.True(body.ActualWidth > 0, window.Title);
                            window.Close();
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
        }
    }
}
