using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The selection box at the top of the Properties palette (and of the Quick Properties panel): a list of All and one row
    /// for each kind of object selected with its count; a kind chosen shows its full properties and is the only one an edit
    /// reaches, while the drawing's selection stays whole.
    /// </summary>
    public sealed class PropertiesKindBoxTests
    {
        /// <summary>Two MTEXTs, two lines and a circle on layer 0, with the layers WALLS and DOORS.</summary>
        private static MainViewModel Open(TestFolder folder, out List<string> everything)
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DOORS" });

            for (int i = 0; i < 2; i++)
            {
                db.ModelSpace.AppendEntity(
                    new DxfMText
                    {
                        Location = new GeoPoint3(i * 100, 200, 0),
                        TextHeight = 2.5,
                        Contents = "T" + i,
                    }
                );
                db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, i * 50, 0), new GeoPoint3(100, i * 50, 0)));
            }

            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(300, 0, 0), GeoVector3.ZAxis, 25));
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("kinds.dxf", db)));
            everything = model.Document.ModelSpace.Block.Select(e => e.Handle).ToList();
            return model;
        }

        private static List<string> Of<T>(MainViewModel model)
            where T : DxfEntity => model.Document.ModelSpace.Block.OfType<T>().Select(e => e.Handle).ToList();

        private static PropertyRowViewModel Row(MainViewModel model, string group, string label) =>
            model.PropertyGroups.Single(g => g.Title == group).Rows.Single(r => r.Label == label);

        [Fact]
        public void AMixedSelection_ListsAllAndOneRowForEachKind_AndShowsTheCommonRows()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);

                model.Document.Selection.Set(all);

                Assert.Equal(new[] { "All (5)", "Circle (1)", "Line (2)", "MText (2)" }, model.SelectionKinds);
                Assert.Equal("All (5)", model.SelectedKind);
                Assert.Equal("All (5)", model.SelectionLabel);
                Assert.Equal(new[] { "General" }, model.PropertyGroups.Select(g => g.Title));
            }
        }

        [Fact]
        public void ChoosingAKind_ShowsItsFullProperties_WhileTheSelectionOfTheDrawingStaysWhole()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.Document.Selection.Set(all);

                model.SelectedKind = "Line (2)";

                Assert.Equal("Line (2)", model.SelectedKind);
                Assert.Equal("Line (2)", model.SelectionLabel);
                Assert.Contains(model.PropertyGroups, g => g.Title == "Geometry");
                Assert.Equal("100.0000", Row(model, "Geometry", "Length").Value);
                Assert.Equal(5, model.Document.Selection.Count);
                Assert.Equal(new[] { "All (5)", "Circle (1)", "Line (2)", "MText (2)" }, model.SelectionKinds);

                model.SelectedKind = "All (5)";

                Assert.Equal("All (5)", model.SelectionLabel);
                Assert.Equal(new[] { "General" }, model.PropertyGroups.Select(g => g.Title));
            }
        }

        [Fact]
        public void AnEditWithAKindChosen_TouchesOnlyThatKind_AsOneUndoStep_AndTheKindStaysChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.Document.Selection.Set(all);
                model.SelectedKind = "Line (2)";

                Row(model, "General", "Layer").Value = "WALLS";

                Assert.All(Of<DxfLine>(model), h => Assert.Equal("WALLS", model.Document.FindEntity(h).Layer));
                Assert.All(Of<DxfMText>(model), h => Assert.Equal("0", model.Document.FindEntity(h).Layer));
                Assert.All(Of<DxfCircle>(model), h => Assert.Equal("0", model.Document.FindEntity(h).Layer));
                Assert.Equal(5, model.Document.Selection.Count);
                Assert.Equal("Line (2)", model.SelectedKind);
                Assert.Equal("WALLS", Row(model, "General", "Layer").Value);

                Assert.True(model.Undo());

                Assert.All(Of<DxfLine>(model), h => Assert.Equal("0", model.Document.FindEntity(h).Layer));
            }
        }

        [Fact]
        public void AnEditWithAllChosen_ReachesEveryObjectSelected()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.Document.Selection.Set(all);

                Row(model, "General", "Layer").Value = "DOORS";

                Assert.All(all, h => Assert.Equal("DOORS", model.Document.FindEntity(h).Layer));
            }
        }

        [Fact]
        public void TheRows_FollowTheSelection_AndTheChoiceReturnsToAllWhenTheSelectionChanges()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.Document.Selection.Set(all);
                model.SelectedKind = "MText (2)";

                model.Document.Selection.Set(all.Take(4).ToList());

                Assert.Equal("All (4)", model.SelectedKind);
                Assert.Equal(model.SelectionKinds.Count, model.SelectionKinds.Distinct().Count());
                Assert.Equal(new[] { "All (4)", "Line (2)", "MText (2)" }, model.SelectionKinds);

                model.Document.Selection.Set(Of<DxfLine>(model));

                Assert.Equal(new[] { "Line (2)" }, model.SelectionKinds);
                Assert.Equal("Line (2)", model.SelectedKind);

                model.Document.Selection.Clear();

                Assert.Equal(new[] { "No selection" }, model.SelectionKinds);
                Assert.Equal("No selection", model.SelectedKind);
            }
        }

        [Fact]
        public void TheQuickPropertiesPanel_ShowsAndEditsTheKindChosen_AsThePaletteDoes()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.QuickPropertiesMode = 1;
                model.Document.Selection.Set(all);
                Assert.True(model.IsQuickPropertiesShown);

                model.SelectedKind = "MText (2)";
                model.QuickPropertyRows.Single(r => r.Label == "Layer").Value = "DOORS";

                Assert.All(Of<DxfMText>(model), h => Assert.Equal("DOORS", model.Document.FindEntity(h).Layer));
                Assert.All(Of<DxfLine>(model), h => Assert.Equal("0", model.Document.FindEntity(h).Layer));
                Assert.Equal("MText (2)", model.SelectedKind);
            }
        }

        [Fact]
        public void ThePickAddToggle_SwitchesPickAddBetweenOneAndNought_AndFollowsSetvar()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _);
                Assert.Equal(1, model.Document.PickAdd);
                Assert.True(model.IsPickAddOn);

                model.IsPickAddOn = false;

                Assert.Equal(0, model.Document.PickAdd);
                Assert.False(model.IsPickAddOn);

                model.IsPickAddOn = true;

                Assert.Equal(1, model.Document.PickAdd);

                model.Execute("PICKADD");
                model.Execute("0");

                Assert.False(model.IsPickAddOn);
            }
        }

        [Fact]
        public void Select_AsksForObjects_AndThoseGivenBecomeTheSelection_WithNoUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out List<string> all);
                model.Document.Selection.Set(Of<DxfCircle>(model));

                model.StartCommand("SELECT");

                Assert.Equal("Select objects:", model.Session.PromptText);
                string[] picked = Of<DxfLine>(model).Concat(Of<DxfMText>(model)).ToArray();
                model.Session.FeedSelection(picked);
                model.Session.Enter();

                Assert.False(model.Session.IsRunning);
                Assert.Equal(
                    picked.OrderBy(h => h, StringComparer.OrdinalIgnoreCase),
                    model.Document.Selection.Handles.OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
                );
                Assert.Equal(new[] { "All (4)", "Line (2)", "MText (2)" }, model.SelectionKinds);
                Assert.False(model.Document.CanUndo);
            }
        }

        [Fact]
        public void InTheWindow_TheSelectionBoxIsAnEnabledDropDown_AndTheThreeButtonsBesideItAreThere()
        {
            using (var folder = new TestFolder())
            {
                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    MainViewModel model = Open(folder, out List<string> all);
                    model.Document.Selection.Set(all);
                    var workspace = new CadWorkspace { DataContext = model };
                    Layout(workspace, 2200, 960);

                    ComboBox box = Descendants(workspace)
                        .OfType<ComboBox>()
                        .Single(c =>
                            "Selected objects".Equals(System.Windows.Automation.AutomationProperties.GetName(c))
                        );
                    Assert.True(box.IsEnabled);
                    Assert.Equal(4, box.Items.Count);
                    Assert.Equal("All (5)", box.SelectedItem);

                    box.SelectedItem = "MText (2)";

                    Assert.Equal("MText (2)", model.SelectedKind);
                    Assert.Equal("MText (2)", model.SelectionLabel);

                    foreach (
                        string tip in new[]
                        {
                            "Toggle value of PICKADD system variable",
                            "Select Objects",
                            "Quick Select",
                        }
                    )
                    {
                        ButtonBase button = Descendants(workspace)
                            .OfType<ButtonBase>()
                            .Single(b => tip.Equals(b.ToolTip));
                        Assert.True(button.IsEnabled, tip);
                        Assert.Equal(Visibility.Visible, button.Visibility);

                        if (tip == "Select Objects")
                        {
                            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            Assert.Equal("Select objects:", model.Session.PromptText);
                        }
                    }
                });
            }
        }

        [Fact]
        public void InTheQuickPropertiesPanel_TheSelectionBoxIsADropDownOverTheSameKinds()
        {
            using (var folder = new TestFolder())
            {
                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    MainViewModel model = Open(folder, out List<string> all);
                    model.Document.Selection.Set(all);
                    var window = new QuickPropertiesWindow(model);
                    var body = (FrameworkElement)window.Content;
                    window.Content = null;
                    body.DataContext = window.DataContext;
                    Layout(body, 240, 400);

                    ComboBox box = Descendants(body)
                        .OfType<ComboBox>()
                        .Single(c =>
                            "Selected objects".Equals(System.Windows.Automation.AutomationProperties.GetName(c))
                        );

                    Assert.True(box.IsEnabled);
                    Assert.Equal(4, box.Items.Count);

                    box.SelectedItem = "Circle (1)";

                    Assert.Equal("Circle (1)", model.SelectedKind);
                    window.Close();
                });
            }
        }

        private static void Layout(FrameworkElement element, double width, double height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
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

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
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
