using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Layer states as AutoCAD 2021 keeps and asks them: -LAYER's stAte option (?, Save, Restore, Edit, Name, Delete, Import,
    /// EXport) and LAYERSTATE's Layer States Manager, each state an XRECORD of ACAD_LAYERSTATES under the layer table's
    /// extension dictionary, written as AutoCAD 2021 wrote the same drawing's.
    /// </summary>
    public class LayerStateTests
    {
        /// <summary>
        /// The .las file AutoCAD 2021's -LAYER State EXport wrote of the state Plan of a drawing with the layers 0, A red, B off
        /// and C frozen, every property restored.
        /// </summary>
        private const string AutoCadPlan =
            "  0\r\nLAYERSTATEDICTIONARY\r\n  0\r\nLAYERSTATE\r\n  1\r\nPlan\r\n 91\r\n2047\r\n301\r\n\r\n290\r\n0\r\n302\r\n0\r\n"
            + "  8\r\nC\r\n 90\r\n10\r\n 62\r\n7\r\n370\r\n-3\r\n  6\r\nContinuous\r\n  2\r\nColor_7\r\n440\r\n0\r\n"
            + "  8\r\nB\r\n 90\r\n9\r\n 62\r\n7\r\n370\r\n-3\r\n  6\r\nContinuous\r\n  2\r\nColor_7\r\n440\r\n0\r\n"
            + "  8\r\nA\r\n 90\r\n8\r\n 62\r\n1\r\n370\r\n-3\r\n  6\r\nContinuous\r\n  2\r\nColor_1\r\n440\r\n0\r\n"
            + "  8\r\n0\r\n 90\r\n8\r\n 62\r\n7\r\n370\r\n-3\r\n  6\r\nContinuous\r\n  2\r\nColor_7\r\n440\r\n0\r\n";

        /// <summary>A drawing of R2013 with the layers 0, A red, B off and C frozen, as the AutoCAD 2021 probe made it.</summary>
        private static CadDocument Abc()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "A", ColorIndex = 1 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "B", ColorIndex = 7 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "C", ColorIndex = 7 });
            db.LayerTable["B"].IsOff = true;
            db.LayerTable["C"].IsFrozen = true;
            return CadDocument.From(db, "abc.dxf");
        }

        private static DxfXrecord State(CadDocument document, string name) => (DxfXrecord)document.Database.LayerStateManager.LayerStatesDictionary.GetAt(name);

        private static string Groups(DxfXrecord record) => string.Join(" ", record.Data.Select(g => g.TypeCode + "=" + g.Value));

        private static string Handle(CadDocument document, string layer) => document.Database.LayerTable[layer].Handle;

        [Fact]
        public void LayerStateSave_WritesTheXrecordAutoCad2021Writes_LastLayerFirst_AndRestoreBringsTheLayersBack()
        {
            CadDocument document = Abc();
            CadCommandSession session = Run(Session(document), "-LAYER", "stAte", "?");
            Assert.Contains("Layer state", session.History);
            Assert.Equal("Enter an option [?/Save/Restore/Edit/Name/Delete/Import/EXport]:", session.PromptText);
            Run(session, "Save", "Plan");
            Assert.Equal("Enter states to change [On/Frozen/Lock/Plot/Newvpfreeze/Color/lineType/lineWeight/TRansparency/plotStyle]:", session.PromptText);
            Assert.Contains("Color               Yes", session.History);
            Run(session, Enter, Enter, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal("-LAYER", document.NextUndo.Name);

            string continuous = document.Database.LinetypeTable["Continuous"].Handle;
            string layer(string name, int flags, int color) => "330=" + Handle(document, name) + " 90=" + flags + " 62=" + color + " 370=-3 331=" + continuous + " 1=Color_" + color + " 440=0";
            Assert.Equal("91=2047 301= 290=0 302=0 " + layer("C", 10, 7) + " " + layer("B", 9, 7) + " " + layer("A", 8, 1) + " " + layer("0", 8, 7), Groups(State(document, "Plan")));
            Assert.Equal(new[] { "Plan" }, document.Database.LayerStateManager.GetLayerStateNames(false, false));
            AssertReopens(document);

            CadLayerEditor.SetColor(document, "A", 3);
            CadLayerEditor.SetOn(document, "B", true);
            Run(Session(document), "-LAYER", "stAte", "Restore", "plan", Enter, Enter);
            Assert.Equal(1, document.Database.LayerTable["A"].ColorIndex);
            Assert.True(document.Database.LayerTable["B"].IsOff);
            Assert.True(document.Database.LayerTable["C"].IsFrozen);

            // Restored last: the flag 0x10000 of its mask, as AutoCAD 2021 wrote 67583.
            Assert.StartsWith("91=67583 ", Groups(State(document, "Plan")));
            Assert.True(document.Undo());
            Assert.Equal(3, document.Database.LayerTable["A"].ColorIndex);
        }

        [Fact]
        public void LayerStateSave_ColorAndPlotSwitchedOff_IsMask2007_AndRestoreLeavesTheColour()
        {
            CadDocument document = Abc();
            CadCommandSession session = Run(Session(document), "-LAYER", "stAte", "Save", "Struct", "Color", "Plot");
            Assert.Contains("Plot                No", session.History);
            Run(session, Enter, Enter, Enter);
            Assert.StartsWith("91=2007 ", Groups(State(document, "Struct")));

            CadLayerEditor.SetColor(document, "A", 3);
            CadLayerEditor.SetOn(document, "B", true);
            Run(Session(document), "-LAYER", "stAte", "Restore", "Struct", Enter, Enter);
            Assert.Equal(3, document.Database.LayerTable["A"].ColorIndex);
            Assert.True(document.Database.LayerTable["B"].IsOff);
        }

        [Fact]
        public void LayerStateEditNameDelete_ChangeTheStateAsAutoCadAsks()
        {
            CadDocument document = Abc();
            Run(Session(document), "-LAYER", "stAte", "Save", "Plan", Enter, Enter, Enter);
            CadCommandSession session = Run(Session(document), "-LAYER", "stAte", "Edit", "Plan");
            Assert.Equal("Enter states to change [On/Frozen/Lock/Plot/Newvpfreeze/Color/lineType/lineWeight/TRansparency/plotStyle]:", session.PromptText);
            Run(session, "Lock", Enter);
            Assert.StartsWith("91=2043 ", Groups(State(document, "Plan")));
            Run(session, "Name", "Plan");
            Assert.Equal("Enter new layer state name:", session.PromptText);
            Run(session, "Plan2", "Delete", "nothing");
            Assert.Equal("Enter name of layer state to delete or [?]:", session.PromptText);
            Run(session, Enter);
            Assert.Contains("Unknown layer state name.", session.History);
            Assert.Equal("Enter an option [?/Save/Restore/Edit/Name/Delete/Import/EXport]:", session.PromptText);
            Assert.Equal(new[] { "Plan2" }, document.Database.LayerStateManager.GetLayerStateNames());
            Run(session, "Delete", "Plan2", Enter, Enter);
            Assert.Empty(document.Database.LayerStateManager.GetLayerStateNames());

            // The edit, the new name and the delete were one run of -LAYER: one undo step, back to Plan as saved.
            Assert.True(document.Undo());
            Assert.Equal(new[] { "Plan" }, document.Database.LayerStateManager.GetLayerStateNames());
            Assert.StartsWith("91=2047 ", Groups(State(document, "Plan")));
        }

        [Fact]
        public void LayerStateExport_WritesTheLasFileAutoCad2021Wrote_AndImportReadsItIntoAnotherDrawing()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-las-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string file = Path.Combine(folder, "plan.las");
                CadDocument document = Abc();
                Run(Session(document), "-LAYER", "stAte", "Save", "Plan", Enter, "EXport", "Plan", file, Enter, Enter);
                Assert.Equal(AutoCadPlan, File.ReadAllText(file));

                CadDocument other = Abc();
                CadLayerEditor.SetColor(other, "A", 5);
                CadCommandSession session = Run(Session(other), "-LAYER", "stAte", "Import");
                Assert.Equal("Enter file name to import <abc>:", session.PromptText);
                Run(session, file, "Restore", "Plan", Enter, Enter);
                Assert.Equal(new[] { "Plan" }, other.Database.LayerStateManager.GetLayerStateNames(false, false));
                Assert.Equal(1, other.Database.LayerTable["A"].ColorIndex);
                AssertReopens(other);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void LayerStateSave_KeepsLockPlotTrueColourAndTransparency_AsAutoCad2021()
        {
            CadDocument document = Abc();
            DxfDatabase db = document.Database;
            document.ApplyEdit("TEST", null, s =>
            {
                s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "D", ColorIndex = 7 });
                s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "E", ColorIndex = 7 });
                s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "F", ColorIndex = 7 });
                s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "G", ColorIndex = 7 });
                s.Database.LayerTable["D"].IsLocked = true;
                s.Database.LayerTable["E"].IsPlottable = false;
                s.Database.LayerTable["F"].Color = DxfColor.FromRgb(10, 20, 30);
                s.Database.LayerTable["G"].Transparency = new DxfTransparency(127);
            });

            Run(Session(document), "-LAYER", "stAte", "Save", "Full", Enter, Enter, Enter);
            string groups = Groups(State(document, "Full"));
            Assert.Contains("330=" + Handle(document, "G") + " 90=8 62=7 370=-3 ", groups);
            Assert.Contains(" 440=33554559 ", groups);
            Assert.Contains(" 440=0 92=-1039526882 ", groups);
            Assert.Contains("330=" + Handle(document, "E") + " 90=0 ", groups);
            Assert.Contains("330=" + Handle(document, "D") + " 90=12 ", groups);

            document.ApplyEdit("TEST", null, s =>
            {
                s.Database.LayerTable["F"].ColorIndex = 4;
                s.Database.LayerTable["G"].Transparency = new DxfTransparency(255);
                s.Database.LayerTable["D"].IsLocked = false;
            });

            Run(Session(document), "-LAYER", "stAte", "Restore", "Full", Enter, Enter);
            Assert.Equal(0x0A141E, db.LayerTable["F"].TrueColor);
            Assert.Equal(127, db.LayerTable["G"].Transparency.Alpha);
            Assert.True(db.LayerTable["D"].IsLocked);
        }

        [Fact]
        public void LayerStateManager_NewMaskRenameAndRestore_AreOneUndoStep_AndSameAsDrawingFollows()
        {
            CadDocument document = Abc();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name != "LAYERSTATE")
                {
                    return;
                }

                var request = (CadLayerStatesRequest)e.Payload;
                Assert.Empty(request.States);
                Assert.Equal("*UNSAVED*", request.Current);
                Assert.True(request.IsModelSpace);
                Assert.True(request.New("Plan - all on", "Every layer on and thawed"));
                Assert.False(request.New("plan - ALL on", string.Empty));
                Assert.Equal("A layer state named plan - ALL on already exists.", request.Message);
                CadLayerStateRow row = Assert.Single(request.States);
                Assert.True(row.SameAsDrawing);
                Assert.Equal("Plan - all on", request.Current);
                Assert.Equal("Every layer on and thawed", row.Description);

                Assert.True(request.SetMask(2015));
                Assert.Equal(2015, request.States[0].Mask);
                Assert.True(request.Rename("Plan"));
                Assert.Equal("Plan", request.Selected);
                e.Handled = true;
            };

            Run(session, "LAS");
            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "Plan" }, document.Database.LayerStateManager.GetLayerStateNames());
            Assert.Equal("LAYERSTATE", document.NextUndo.Name);
            AssertReopens(document);

            CadLayerEditor.SetOn(document, "B", true);
            CadCommandSession again = Session(document);
            again.UiRequested += (sender, e) =>
            {
                var request = (CadLayerStatesRequest)e.Payload;
                Assert.False(Assert.Single(request.States).SameAsDrawing);
                Assert.Equal("*UNSAVED*", request.Current);
                request.Selected = "Plan";
                request.Action = CadDialogAction.Ok;
                e.Handled = true;
            };

            Run(again, "LAYERSTATE");
            Assert.True(document.Database.LayerTable["B"].IsOff);
            Assert.True(document.Undo());
            Assert.False(document.Database.LayerTable["B"].IsOff);
            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        /// <summary>
        /// The layers 0, A red with a lineweight of 0.50, B off and C frozen saved as the state Plan; then A made green and
        /// its lineweight Default, B turned on and C thawed, and on Layout1 two viewports made, the first of them worked through.
        /// </summary>
        private static CadDocument PlanChangedAndTwoViewports(out string first, out string second)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            foreach (string layer in new[] { "A", "B", "C" })
            {
                Assert.True(CadLayerEditor.Add(document, layer).Succeeded);
            }

            Assert.True(CadLayerEditor.SetColor(document, "A", 1).Succeeded);
            Assert.True(CadLayerEditor.SetLineWeight(document, "A", DxfLineWeight.LineWeight050).Succeeded);
            Assert.True(CadLayerEditor.SetOn(document, "B", false).Succeeded);
            Assert.True(CadLayerEditor.SetFrozen(document, "C", true).Succeeded);
            Run(session, "-LAYER", "stAte", "Save", "Plan", Enter, Enter, Enter);
            Assert.False(session.IsRunning);

            Assert.True(CadLayerEditor.SetColor(document, "A", 3).Succeeded);
            Assert.True(CadLayerEditor.SetLineWeight(document, "A", DxfLineWeight.ByLineWeightDefault).Succeeded);
            Assert.True(CadLayerEditor.SetOn(document, "B", true).Succeeded);
            Assert.True(CadLayerEditor.SetFrozen(document, "C", false).Succeeded);
            Run(session, "LINE", P(0, 0), P(2000, 1000), Enter);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            Run(session, "MV", P(10, 150), P(210, 250));
            var viewports = All<DxfViewport>(document.CurrentSpace).Where(v => v.Number != 1).ToList();
            first = viewports[0].Handle;
            second = viewports[1].Handle;
            Assert.True(document.EnterViewport(first));
            return document;
        }

        /// <summary>The Layer States Manager answered by restoring Plan, as viewport overrides or not.</summary>
        private static void RestorePlan(CadDocument document, bool asOverrides, bool turnOffMissing = false)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                var request = (CadLayerStatesRequest)e.Payload;
                Assert.False(request.IsModelSpace);
                request.Selected = "Plan";
                request.AsViewportOverrides = asOverrides;
                request.TurnOffMissing = turnOffMissing;
                request.Action = CadDialogAction.Ok;
                e.Handled = true;
            };

            Run(session, "LAYERSTATE");
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AStateRestoredAsViewportOverrides_OverridesAndFreezesInTheCurrentViewportOnly_AndLeavesTheLayers()
        {
            CadDocument document = PlanChangedAndTwoViewports(out string first, out string second);
            DxfDatabase db = document.Database;

            RestorePlan(document, true);

            // The layers themselves stay as they were changed after the state was saved.
            Assert.Equal(3, db.LayerTable["A"].ColorIndex);
            Assert.Equal(DxfLineWeight.ByLineWeightDefault, db.LayerTable["A"].LineWeight);
            Assert.False(db.LayerTable["B"].IsOff);
            Assert.False(db.LayerTable["C"].IsFrozen);

            // In the viewport worked through A is red and 0.50 wide, and B, off in the state, and C, frozen in it, are frozen.
            DxfViewport one = Entity<DxfViewport>(document, first);
            DxfViewport two = Entity<DxfViewport>(document, second);
            DxfLayerViewportProperties a = db.LayerTable["A"].GetViewportOverrides(one);
            Assert.True(a.IsColorOverridden);
            Assert.Equal(1, a.Color.ColorIndex);
            Assert.True(a.IsLineWeightOverridden);
            Assert.Equal(DxfLineWeight.LineWeight050, a.LineWeight);
            Assert.False(a.IsLinetypeOverridden);
            Assert.False(a.IsTransparencyOverridden);
            Assert.False(db.LayerTable["0"].HasViewportOverrides(one));
            Assert.Equal(new[] { "B", "C" }, one.GetFrozenLayers().Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal));

            // The other viewport has none of it.
            Assert.False(db.LayerTable["A"].HasViewportOverrides(two));
            Assert.Empty(two.GetFrozenLayers());
            Assert.Equal("LAYERSTATE", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.False(document.Database.LayerTable["A"].HasViewportOverrides(Entity<DxfViewport>(document, first)));
            Assert.Empty(Entity<DxfViewport>(document, first).GetFrozenLayers());
            Assert.True(document.Redo());
            Assert.Equal(1, document.Database.LayerTable["A"].GetViewportOverrides(Entity<DxfViewport>(document, first)).Color.ColorIndex);
            Assert.Equal(2, Entity<DxfViewport>(document, first).GetFrozenLayers().Count);
        }

        [Fact]
        public void AStateRestoredAsViewportOverridesAgain_TakesOffAnOverrideTheLayerNoLongerNeeds_AndThawsWhatTheStateHasOn()
        {
            CadDocument document = PlanChangedAndTwoViewports(out string first, out _);
            DxfDatabase db = document.Database;
            RestorePlan(document, true);

            // The layer made red again and B kept off in a second state of the same name: the override is taken off, B stays frozen there.
            Assert.True(CadLayerEditor.SetColor(document, "A", 1).Succeeded);
            RestorePlan(document, true);
            DxfViewport one = Entity<DxfViewport>(document, first);
            Assert.False(db.LayerTable["A"].GetViewportOverrides(one).IsColorOverridden);
            Assert.True(db.LayerTable["A"].GetViewportOverrides(one).IsLineWeightOverridden);

            // A layer frozen in the viewport by hand that the state has on and thawed is thawed there.
            Assert.True(CadViewportLayerEditor.SetFrozen(document, new[] { first }, new[] { "A" }, true).Succeeded);
            RestorePlan(document, true);
            Assert.False(Entity<DxfViewport>(document, first).IsLayerFrozenInViewport(db.LayerTable["A"]));
        }

        [Fact]
        public void AStateRestoredAsViewportOverrides_WithTheLayersNotFoundTurnedOff_FreezesThemInTheViewport()
        {
            CadDocument document = PlanChangedAndTwoViewports(out string first, out _);
            Assert.True(CadLayerEditor.Add(document, "LATER").Succeeded);

            RestorePlan(document, true, true);

            Assert.False(document.Database.LayerTable["LATER"].IsOff);
            Assert.True(Entity<DxfViewport>(document, first).IsLayerFrozenInViewport(document.Database.LayerTable["LATER"]));
        }

        [Fact]
        public void AStateRestoredInAViewportWithoutTheOverridesTicked_ChangesTheLayers_AsOnTheModelTab()
        {
            CadDocument document = PlanChangedAndTwoViewports(out string first, out _);

            RestorePlan(document, false);

            Assert.Equal(1, document.Database.LayerTable["A"].ColorIndex);
            Assert.True(document.Database.LayerTable["B"].IsOff);
            Assert.False(document.Database.LayerTable["A"].HasViewportOverrides(Entity<DxfViewport>(document, first)));
            Assert.Empty(Entity<DxfViewport>(document, first).GetFrozenLayers());
        }
    }
}
