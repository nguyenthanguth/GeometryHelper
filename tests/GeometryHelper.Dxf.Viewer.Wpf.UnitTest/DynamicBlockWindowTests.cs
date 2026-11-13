using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Dynamic blocks in the window, to the design DynBlocks: the Custom group of Properties (lists as drop-downs, distances
    /// typed), a flip grip clicked, a visibility grip's list with the current state ticked, the right-click menu's Reset Block,
    /// and the Block Editor's Visibility State box live for a block with states. Nothing opens a real window.
    /// </summary>
    public sealed class DynamicBlockWindowTests
    {
        /// <summary>
        /// A drawing of GH_PANEL, a rectangle whose Width (100, 150 or 200) stretches its right edge, GH_DOOR, a leaf flipped by
        /// Hand, and GH_SIGN, a cross and a tick of the states Cross, Tick and Blank; one INSERT of each, in that order.
        /// </summary>
        private static CadDocument Plan(out string[] handles)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            var panel = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(panel);
            DxfLine[] sides =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
            };

            foreach (DxfLine side in sides)
            {
                panel.AppendEntity(side);
            }

            DxfBlockLinearParameter width = panel.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), sides);

            var door = new DxfBlockTableRecord { Name = "GH_DOOR", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(door);
            var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
            door.AppendEntity(leaf);
            door.AddFlipAction(
                door.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)),
                new[] { leaf }
            );

            var sign = new DxfBlockTableRecord { Name = "GH_SIGN", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(sign);
            var cross = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0));
            var tick = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0));
            sign.AppendEntity(cross);
            sign.AppendEntity(tick);
            sign.AddVisibilityParameter(
                "Mark",
                null,
                new GeoPoint3(20, -10, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { cross }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { tick }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                }
            );

            var inserts = new[]
            {
                new DxfBlockReference(new GeoPoint3(0, 0, 0), panel),
                new DxfBlockReference(new GeoPoint3(500, 0, 0), door),
                new DxfBlockReference(new GeoPoint3(1000, 0, 0), sign),
            };

            foreach (DxfBlockReference insert in inserts)
            {
                database.ModelSpace.AppendEntity(insert);
            }

            handles = inserts.Select(i => i.Handle).ToArray();
            return CadDocument.From(database, "dynamic.dxf");
        }

        private static MainViewModel Open(CadDocument document)
        {
            var model = new MainViewModel();
            model.OpenDocument(document);
            return model;
        }

        private static PropertyGroupViewModel Custom(MainViewModel model) =>
            model.PropertyGroups.SingleOrDefault(g => g.Title == "Custom");

        private static object ValueOf(CadDocument document, string handle, string name) =>
            ((DxfBlockReference)document.FindEntity(handle))
                .DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name)
                .Value;

        [Fact]
        public void TheCustomGroup_StandsBetweenGeometryAndMisc_TheWidthTypedSnapsToItsList()
        {
            CadDocument document = Plan(out string[] handles);
            MainViewModel model = Open(document);
            document.Selection.Set(new[] { handles[0] });

            Assert.Equal(
                new[] { "General", "Geometry", "Custom", "Misc" },
                model.PropertyGroups.Select(g => g.Title).Take(4)
            );
            PropertyRowViewModel width = Custom(model).Rows.Single(r => r.Label == "Width");
            Assert.True(width.IsTyped);
            Assert.Equal("100.0000", width.Value);

            width.Value = "180";

            Assert.Equal(200.0, (double)ValueOf(document, handles[0], "Width"), 9);
            Assert.Equal("200.0000", Custom(model).Rows.Single(r => r.Label == "Width").Value);
            Assert.Equal(
                "GH_PANEL",
                model.PropertyGroups.Single(g => g.Title == "Misc").Rows.Single(r => r.Label == "Name").Value
            );
        }

        [Fact]
        public void AFlipAndAVisibilityProperty_AreDropDowns_OfTheStateNames()
        {
            CadDocument document = Plan(out string[] handles);
            MainViewModel model = Open(document);

            document.Selection.Set(new[] { handles[1] });
            PropertyRowViewModel hand = Custom(model).Rows.Single(r => r.Label == "Hand");
            Assert.True(hand.HasOptions);
            Assert.Equal(new[] { "Not flipped", "Flipped" }, hand.Options);
            hand.Value = "Flipped";
            Assert.Equal((short)1, ValueOf(document, handles[1], "Hand"));

            document.Selection.Set(new[] { handles[2] });
            PropertyRowViewModel mark = Custom(model).Rows.Single(r => r.Label == "Mark");
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, mark.Options);
            mark.Value = "Blank";
            Assert.Equal("Blank", ValueOf(document, handles[2], "Mark"));
        }

        [Fact]
        public void TheFlipGripClicked_FlipsAtOnce_AndTheVisibilityGripListsItsStates_TheCurrentTicked()
        {
            CadDocument document = Plan(out string[] handles);
            MainViewModel model = Open(document);

            Assert.Null(model.ClickCustomGrip(handles[1], 0));
            Assert.Equal((short)1, ValueOf(document, handles[1], "Hand"));

            IReadOnlyList<CadMenuItemViewModel> states = model.ClickCustomGrip(handles[2], 0);
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, states.Select(s => s.Header));
            Assert.Equal(new[] { true, false, false }, states.Select(s => s.IsChecked));

            states[1].Command.Execute(null);

            Assert.Equal("Tick", ValueOf(document, handles[2], "Mark"));
            Assert.Equal(new[] { false, true, false }, model.ClickCustomGrip(handles[2], 0).Select(s => s.IsChecked));
            Assert.True(document.Undo());
            Assert.Equal("Cross", ValueOf(document, handles[2], "Mark"));
        }

        [Fact]
        public void TheRightClickMenu_OffersResetBlock_ForADynamicSelection_WhichPutsItBack()
        {
            CadDocument document = Plan(out string[] handles);
            MainViewModel model = Open(document);
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handles[0] }, "Width", "200").Succeeded);
            Assert.Null(model.ShortcutMenu());

            document.Selection.Set(new[] { handles[0] });
            CadMenuItemViewModel reset = Assert.Single(model.ShortcutMenu());
            Assert.Equal("Reset Block", reset.Header);

            reset.Command.Execute(null);

            Assert.Equal(100.0, (double)ValueOf(document, handles[0], "Width"), 9);
            Assert.Equal("GH_PANEL", ((DxfBlockReference)document.FindEntity(handles[0])).Name);
        }

        [Fact]
        public void TheBlockEditorsVisibilityStateBox_IsLive_ForABlockWithStates()
        {
            CadDocument document = Plan(out _);
            MainViewModel model = Open(document);
            model.Execute("-BEDIT");
            model.Execute("GH_SIGN");
            var ribbon = (BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent;

            Assert.True(ribbon.HasVisibilityStates);
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, ribbon.VisibilityStates);
            Assert.Equal("Cross", ribbon.VisibilityState);

            ribbon.VisibilityState = "Tick";

            Assert.Equal("Tick", document.BlockEditor.VisibilityState);
            Assert.Equal("Tick", ribbon.VisibilityState);
            Assert.False(ribbon.HasUnsavedChanges);

            model.Execute("BCLOSE");
            model.Execute("-BEDIT");
            model.Execute("GH_PANEL");
            ribbon = (BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent;
            Assert.False(ribbon.HasVisibilityStates);
            Assert.Equal(new[] { "(none)" }, ribbon.VisibilityStates);
        }
    }
}
