using System;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Layer manager's VP columns and the status bar's MODEL/PAPER button, scale and padlock, on the room whose Layout1
    /// holds a viewport 200 by 150 showing it at 1:20; no window shown.
    /// </summary>
    public class ViewportLayersAndStatusTests
    {
        private static void OnRoom(Action<MainViewModel, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport)));
                        test(model, viewport);
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

        private static CadSpace Layout1(MainViewModel model) => model.Document.Spaces.Single(s => s.Name == "Layout1");

        [Fact]
        public void TheVpColumns_ShowOnlyInALayout_AndActOnTheViewportWorkedThrough()
        {
            OnRoom((model, viewport) =>
            {
                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.False(manager.HasViewportColumns);
                    Assert.Equal(900, manager.WindowWidth);
                }

                model.SelectSpace(Layout1(model));
                model.Document.EnterViewport(viewport);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.True(manager.HasViewportColumns);
                    Assert.True(manager.HasTargetViewport);
                    Assert.Equal(1400, manager.WindowWidth);
                    Assert.EndsWith("Layout1, viewport " + ((DxfViewport)model.Document.FindEntity(viewport)).Number + " 1:20 current", manager.Title);

                    LayerRowViewModel walls = manager.Rows.Single(r => r.Name == "WALLS");
                    walls.IsVpFrozen = true;
                    Assert.True(((DxfViewport)model.Document.FindEntity(viewport)).IsLayerFrozenInViewport(model.Document.Database.LayerTable["WALLS"]));
                    walls = manager.Rows.Single(r => r.Name == "WALLS");
                    Assert.True(walls.IsVpFrozen);
                    Assert.StartsWith("3 layers · 1 frozen in this viewport · 0 overrides", manager.Summary);

                    LayerRowViewModel columns = manager.Rows.Single(r => r.Name == "COLUMNS");
                    columns.VpWeight = "0.50";
                    columns.VpTransparency = "50";
                    columns = manager.Rows.Single(r => r.Name == "COLUMNS");
                    Assert.True(columns.IsVpWeightOverridden);
                    Assert.Equal("0.50", columns.VpWeight);
                    Assert.Equal("50", columns.VpTransparency);
                    Assert.Equal(127, model.Document.Database.LayerTable["COLUMNS"].GetViewportOverrides((DxfViewport)model.Document.FindEntity(viewport)).Transparency.Alpha);
                    Assert.Equal("Color_1", columns.VpPlotStyle);
                    Assert.Equal("3 layers · 1 frozen in this viewport · 2 overrides", manager.Summary);

                    columns.IsChosen = true;
                    manager.RemoveOverridesCommand.Execute("Selected,Current");
                    Assert.False(manager.Rows.Single(r => r.Name == "COLUMNS").IsVpWeightOverridden);

                    manager.Rows.Single(r => r.Name == "COLUMNS").IsChosen = true;
                    manager.VpFreezeAllCommand.Execute(null);
                    Assert.True(manager.Rows.Single(r => r.Name == "COLUMNS").IsVpFrozen);
                    Assert.NotNull(manager.NewLayerVpFrozen());
                    Assert.True(manager.SelectedRow.IsVpFrozen);
                }
            });
        }

        [Fact]
        public void OnThePaper_TheViewportSelected_IsTheOneTheColumnsActOn_AndNoneLeavesThemGrey()
        {
            OnRoom((model, viewport) =>
            {
                model.SelectSpace(Layout1(model));

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.True(manager.HasViewportColumns);
                    Assert.False(manager.HasTargetViewport);
                    manager.Rows.Single(r => r.Name == "WALLS").IsVpFrozen = true;
                    Assert.False(manager.Rows.Single(r => r.Name == "WALLS").IsVpFrozen);
                }

                model.Document.Selection.Set(new[] { viewport });

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.True(manager.HasTargetViewport);
                }
            });
        }

        [Fact]
        public void TheStatusBar_ShowsTheAnnotationScaleOnTheModelTab_TheViewportsScaleAndPadlockInALayout()
        {
            OnRoom((model, viewport) =>
            {
                // 1: the Model tab, MODEL a label, the annotation scale.
                Assert.False(model.IsSpaceButton);
                Assert.True(model.HasStatusScale);
                Assert.Equal("1:1", model.StatusScale);
                Assert.False(model.HasViewportScale);
                Assert.Contains(model.ScaleMenu(), i => i.Header == "1:1" && i.IsChecked);

                // 2: the paper, nothing selected: PAPER a button, no scale, no padlock.
                model.SelectSpace(Layout1(model));
                Assert.True(model.IsSpaceButton);
                Assert.Equal("PAPER", model.SpaceLabel);
                Assert.False(model.HasStatusScale);
                Assert.Empty(model.ScaleMenu());

                // 3: a locked viewport selected: its scale, the padlock closed, every scale grey.
                model.Document.Selection.Set(new[] { viewport });
                model.ToggleViewportLockCommand.Execute(null);
                Assert.True(((DxfViewport)model.Document.FindEntity(viewport)).Locked);
                Assert.True(model.IsViewportLocked);
                Assert.Equal("1:20", model.StatusScale);
                Assert.All(model.ScaleMenu().Where(i => i.Header.Length > 0), i => Assert.False(i.IsEnabled));

                // 4: through the viewport, unlocked: MODEL, its scale, the list as AutoCAD's.
                model.ToggleViewportLockCommand.Execute(null);
                model.Document.EnterViewport(viewport);
                Assert.Equal("MODEL", model.SpaceLabel);
                Assert.False(model.IsViewportLocked);
                var headers = model.ScaleMenu().Select(i => i.Header).ToList();
                Assert.Equal("Scale to fit", headers[0]);
                Assert.Equal("Custom...", headers[headers.Count - 1]);
                Assert.Contains(model.ScaleMenu(), i => i.Header == "1:20" && i.IsChecked);

                model.ScaleMenu().Single(i => i.Header == "1:50").Command.Execute(null);
                Assert.Equal("1:50", model.StatusScale);
            });
        }

        [Fact]
        public void Custom_EditsTheDrawingsScaleList_WhichTheListThenOffers()
        {
            OnRoom((model, viewport) =>
            {
                model.SelectSpace(Layout1(model));
                model.Document.EnterViewport(viewport);

                // Custom... runs -SCALELISTEDIT, as AutoCAD's opens the drawing's scales: a scale added there is in the list.
                model.ScaleMenu().Single(i => i.Header == "Custom...").Command.Execute(null);
                model.Execute("A");
                Assert.Contains(model.History, l => l.StartsWith("Enter option [?/Add/Delete/Reset/Exit] <Add>", StringComparison.Ordinal));
                model.Execute("Plan 1:37.5");
                model.Execute("1:37.5");
                model.Execute("E");

                model.ScaleMenu().Single(i => i.Header == "Plan 1:37.5").Command.Execute(null);
                Assert.Equal("1:37.5", model.StatusScale);
                Assert.Contains(model.ScaleMenu(), i => i.Header == "Plan 1:37.5" && i.IsChecked);
                Assert.Equal(150 * 37.5, ((DxfViewport)model.Document.FindEntity(viewport)).ViewHeight, 9);
            });
        }
    }
}
