using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The commands behind the dialogs of the "dialogs-ui" round, driven with no window: SCALELISTEDIT's request and its
    /// fallback to -SCALELISTEDIT, Reset by MEASUREMENT, a layer renamed by <see cref="CadLayerEditor.Rename"/> with AutoCAD
    /// 2021's refusals, and a layer state edited through LAYERSTATE's request.
    /// </summary>
    public class DialogsUiCommandTests
    {
        private static DxfObjectContextCollection Scales(CadDocument document) =>
            document.Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

        private static string[] Names(CadDocument document) => Scales(document).Select(s => s.Name).ToArray();

        // ---- SCALELISTEDIT

        [Fact]
        public void ResetInAnImperialDrawing_PutsBackTheImperialScales_AfterTheOnesKept_AsAutoCADsConsole()
        {
            // AutoCAD 2021's console, MEASUREMENT 0 then -SCALELISTEDIT Reset Yes ?: 1:1 (kept), then 1/128" = 1'-0" (0.0078
            // to 12) to 1'-0" = 1'-0" (12 to 12), 17 in all.
            CadDocument document = Blank();
            document.Database.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            CadCommandSession session = Session(document);
            Run(session, "-SCALELISTEDIT", "R", "Y", "E");
            Assert.Equal(CadViewports.StandardScales, Names(document));

            document.Database.Measurement = DxfMeasurementValue.English;
            Run(session, "-SCALELISTEDIT", "R", "Y", "E");

            string[] names = Names(document);
            Assert.Equal(17, names.Length);
            Assert.Equal("1:1", names[0]);
            Assert.Equal("1/128\" = 1'-0\"", names[1]);
            Assert.Equal("1'-0\" = 1'-0\"", names[16]);
            var smallest = (DxfAnnotationScale)Scales(document).GetContext("1/128\" = 1'-0\"");
            Assert.Equal(0.0078125, smallest.PaperUnits);
            Assert.Equal(12.0, smallest.DrawingUnits);
            Assert.DoesNotContain("1:50", names);
        }

        [Fact]
        public void ResetInADrawingWithNoMeasurement_PutsBackTheImperialScales_AsAutoCADReadsSuchAHeader()
        {
            // AutoCAD 2021 reads a DXF whose header has no $MEASUREMENT as MEASUREMENT 0 (English), as a new Database is
            // (probe GHCMU1: Database.DxfIn of such a file gives English, of one with $MEASUREMENT 1 gives Metric), so Reset
            // puts back the imperial list there, not the metric one.
            CadDocument document = Blank();
            Assert.Null(document.Database.GetHeader("$MEASUREMENT"));
            CadCommandSession session = Session(document);

            Run(session, "-SCALELISTEDIT", "R", "Y", "E");

            string[] names = Names(document);
            Assert.Equal(17, names.Length);
            Assert.Equal("1/128\" = 1'-0\"", names[1]);
            Assert.DoesNotContain("1:50", names);
        }

        [Fact]
        public void ScaleListEdit_WithNoWindow_AsksTheTypedPrompts()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "SCALELISTEDIT");

            Assert.Equal("Enter option [?/Add/Delete/Reset/Exit] <Add>:", session.PromptText);
        }

        [Fact]
        public void ScaleListEdit_OkWritesTheDialogsList_DeletedRenamedMovedAndAdded_AsOneUndoStep()
        {
            CadDocument document = Blank();
            document.Database.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            CadCommandSession session = Session(document);
            Run(session, "-SCALELISTEDIT", "R", "Y", "E");
            string[] before = Names(document);
            CadScaleListRequest seen = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadScaleListRequest request)
                {
                    seen = request;
                    Assert.Equal(before, request.Scales.Select(r => r.Name));
                    Assert.True(request.Scales[0].IsKept);
                    Assert.False(request.Scales[0].IsInUse);
                    Assert.False(request.Scales.Single(r => r.Name == "1:50").IsKept);
                    Assert.Equal(17, request.Defaults.Count);

                    // Reset's three buttons: the metric list, the imperial one, and both in one without 1:1 twice.
                    Assert.Equal(17, request.MetricDefaults.Count);
                    Assert.Equal("100:1", request.MetricDefaults.Last().Name);
                    Assert.Equal(17, request.ImperialDefaults.Count);
                    Assert.Equal("1:1", request.ImperialDefaults[0].Name);
                    Assert.Equal("1'-0\" = 1'-0\"", request.ImperialDefaults.Last().Name);
                    Assert.Equal(33, request.MetricAndImperialDefaults.Count);
                    Assert.Equal(
                        request
                            .MetricDefaults.Select(r => r.Name)
                            .Concat(request.ImperialDefaults.Skip(1).Select(r => r.Name)),
                        request.MetricAndImperialDefaults.Select(r => r.Name)
                    );

                    // 1:2 and 1:4 swap names, 1:50 goes first, 1:100 is deleted, 1:25 is added at the end.
                    var rows = request.Scales.Where(r => r.Name != "1:100").ToList();
                    int two = rows.FindIndex(r => r.Name == "1:2");
                    int four = rows.FindIndex(r => r.Name == "1:4");
                    rows[two] = rows[two].With("1:4", 1, 4);
                    rows[four] = rows[four].With("1:2", 1, 2);
                    var fifty = rows.Single(r => r.Name == "1:50");
                    rows.Remove(fifty);
                    rows.Insert(0, fifty);
                    rows.Add(new CadScaleListRow("1:25", 1, 25));
                    request.Scales = rows;
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "SCALELISTEDIT");

            Assert.NotNull(seen);
            Assert.False(session.IsRunning);
            string[] names = Names(document);
            Assert.Equal("1:50", names[0]);
            Assert.Equal("1:25", names.Last());
            Assert.DoesNotContain("1:100", names);
            Assert.Equal(17, names.Length);
            var four4 = (DxfAnnotationScale)Scales(document).GetContext("1:4");
            Assert.Equal(4.0, four4.DrawingUnits);
            Assert.Equal("SCALELISTEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(before, Names(document));
        }

        [Fact]
        public void ScaleListEdit_CancelOrNoChange_WritesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-SCALELISTEDIT", "R", "Y", "E");
            object undo = document.NextUndo;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadScaleListRequest request)
                {
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "SCALELISTEDIT");

            Assert.Same(undo, document.NextUndo);
        }

        // ---- Rename of a layer

        [Fact]
        public void ALayerRenamed_TakesItsObjectsAlong_AndIsRefusedInAutoCADsWords()
        {
            CadDocument document = Blank();
            DxfDatabase db = document.Database;
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "TEXT" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DIM" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Defpoints" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "TEXT" });

            // AutoCAD 2021's console, -RENAME LAyer: 0 refused, a name used refused, < refused, Defpoints renamed.
            Assert.Equal("Cannot rename layer \"0\".", CadLayerEditor.Rename(document, "0", "X").Message);
            Assert.Equal(
                "A layer named \"DIM\" already exists.",
                CadLayerEditor.Rename(document, "TEXT", "dim").Message
            );
            Assert.Equal("Invalid layer name.", CadLayerEditor.Rename(document, "TEXT", "A<B").Message);
            Assert.Equal("Invalid layer name.", CadLayerEditor.Rename(document, "TEXT", "A,B").Message);
            Assert.Equal("Invalid layer name.", CadLayerEditor.Rename(document, "TEXT", "   ").Message);
            Assert.Equal("Cannot find layer \"NOPE\".", CadLayerEditor.Rename(document, "NOPE", "Y").Message);
            Assert.True(CadLayerEditor.Rename(document, "Defpoints", "DP2").Succeeded);

            // The spaces around the name taken off; then letters of another case taken.
            Assert.True(CadLayerEditor.Rename(document, "TEXT", "  T2  ").Succeeded);
            Assert.Equal("T2", db.ModelSpace.OfType<DxfLine>().Single().Layer);
            Assert.True(CadLayerEditor.Rename(document, "T2", "t2").Succeeded);
            Assert.True(db.LayerTable.Has("t2"));
            Assert.Equal("t2", db.LayerTable["T2"].Name);
            Assert.Equal("LAYER", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("T2", document.Database.LayerTable["t2"].Name);
        }

        // ---- Edit Layer State

        [Fact]
        public void EditLayerState_ChangesTheStateOnly_LayersAddedAndRemoved_AsAnEditOfLayerstate()
        {
            CadDocument document = Blank();
            DxfDatabase db = document.Database;
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS", ColorIndex = 1 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DIM", ColorIndex = 3 });
            CadCommandSession session = Session(document);
            session.UiRequested += (s, e) =>
            {
                if (!(e.Payload is CadLayerStatesRequest request))
                {
                    return;
                }

                e.Handled = true;
                Assert.True(request.New("Plan", "All on"));
                Assert.True(request.Has("plan"));
                request.Selected = "Plan";
                CadLayerStateEdit edit = request.BeginEdit();
                Assert.Equal(new[] { "0", "DIM", "WALLS" }, edit.Layers.Select(l => l.Name));
                Assert.Empty(edit.LayersNotInState());

                CadLayerStateLayer dim = edit.Layers.Single(l => l.Name == "DIM");
                Assert.False(edit.Differs(dim));
                dim.IsOn = false;
                dim.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5);
                dim.Transparency = 40;
                Assert.True(edit.Differs(dim));
                edit.Layers.Remove(edit.Layers.Single(l => l.Name == "WALLS"));
                Assert.Equal(new[] { "WALLS" }, edit.LayersNotInState());
                edit.Mask &= ~0x40;
                Assert.True(request.Edit(edit));

                // Read again: DIM off and blue at 40 per cent, WALLS gone, linetype no longer restored.
                CadLayerStateEdit again = request.BeginEdit();
                CadLayerStateLayer read = again.Layers.Single(l => l.Name == "DIM");
                Assert.False(read.IsOn);
                Assert.Equal(5, read.Color.ColorIndex);
                Assert.Equal(40, read.Transparency);
                Assert.Equal(new[] { "0", "DIM" }, again.Layers.Select(l => l.Name));
                Assert.Equal(0, again.Mask & 0x40);
                Assert.NotNull(again.Add("WALLS"));
                Assert.Null(again.Add("WALLS"));
                Assert.True(request.Edit(again));

                // New over a name used replaces the state, its description too.
                Assert.True(request.Replace("plan", "Replaced"));
                Assert.Equal("Replaced", request.States.Single().Description);
            };

            Run(session, "LAYERSTATE");

            // The drawing's DIM is untouched: only the state changed.
            Assert.False(db.LayerTable["DIM"].IsOff);
            Assert.Equal(3, db.LayerTable["DIM"].ColorIndex);
            Assert.Equal("LAYERSTATE", document.NextUndo.Name);
        }
    }
}
