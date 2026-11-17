using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// LAYTRANS, AutoCAD 2021's Layer Translator: the layers of the drawing mapped to the layers of a standards file and
    /// translated, every object of a layer moved to the layer it is mapped to, the layers translated from deleted, the layers
    /// translated to made or given the standards' settings, the settings of the translator obeyed.
    /// </summary>
    public sealed class LayerTranslatorTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "layer3-tests",
            Guid.NewGuid().ToString("N")
        );

        public LayerTranslatorTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Layers OLD-WALL (1), OLD-DOOR (2) and KEEP (5); in model space A1 on OLD-WALL of colour 30 and linetype HIDDEN, A2 on
        /// OLD-DOOR, A3 on KEEP, A5 an INSERT of BLK; the block BLK holds a line on OLD-WALL and one on KEEP.
        /// </summary>
        private static DxfDatabase Plan() =>
            TestDrawings.DrawingWithLinetypes(
                TestDrawings.Linetype("HIDDEN", 4, -2),
                TestDrawings.Layer("OLD-WALL", 1) + TestDrawings.Layer("OLD-DOOR", 2) + TestDrawings.Layer("KEEP", 5),
                TestDrawings.Block(
                    "BLK",
                    TestDrawings.Line(0, 0, 10, 0, "OLD-WALL", 30),
                    TestDrawings.Line(0, 5, 10, 5, "KEEP")
                ),
                TestDrawings.Entity(
                    "LINE",
                    "OLD-WALL",
                    30,
                    "A1",
                    "6",
                    "HIDDEN",
                    "10",
                    0.0,
                    "20",
                    0.0,
                    "30",
                    0.0,
                    "11",
                    100.0,
                    "21",
                    0.0,
                    "31",
                    0.0
                ),
                TestDrawings.Line(0, 10, 100, 10, "OLD-DOOR", handle: "A2"),
                TestDrawings.Line(0, 20, 100, 20, "KEEP", handle: "A3"),
                TestDrawings.Insert("BLK", 200, 0, handle: "A5")
            );

        /// <summary>A standards file of WALL (3, DASHED), DOOR (4) and KEEP (6), as a DXF.</summary>
        private string Standards(string name = "office.dxf")
        {
            DxfDatabase file = TestDrawings.DrawingWithLinetypes(
                TestDrawings.Linetype("DASHED", 12, -6),
                TestDrawings.Layer("WALL", 3, linetype: "DASHED")
                    + TestDrawings.Layer("DOOR", 4)
                    + TestDrawings.Layer("KEEP", 6),
                string.Empty
            );
            string path = Path.Combine(_folder, name);
            file.Save(path);
            return path;
        }

        private static CadLayerTranslatorRequest Request(CadDocument document)
        {
            CadLayerTranslatorRequest request = null;
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "LAYTRANS")
                {
                    request = (CadLayerTranslatorRequest)e.Payload;
                    request.Action = CadDialogAction.Cancel;
                    e.Handled = true;
                }
            };
            Run(session, "LAYTRANS");
            return request;
        }

        /// <summary>The plan as a drawing of AutoCAD 2013, which has true colours and transparency.</summary>
        private static DxfDatabase ModernPlan() =>
            DxfDatabase.Parse(Plan().ToText().Replace("\n1\nAC1015\n", "\n1\nAC1027\n"));

        [Fact]
        public void ADrawingOlderThanR2004_TakesAnIndexedColourAndNoTransparency_AndTranslatesAllTheSame()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Map(new[] { "OLD-DOOR" }, "DOOR");
            request.Edit(
                new[] { "OLD-WALL" },
                new CadTranslateLayer("x") { Color = DxfColor.FromRgb(1, 2, 3), TransparencyPercent = 50 }
            );
            request.Edit(
                new[] { "OLD-DOOR" },
                new CadTranslateLayer("x") { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 40) }
            );

            CadEditResult result = request.Translate();

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("WALL", document.FindEntity("A1").Layer);
            Assert.False(document.Database.LayerTable["WALL"].Color.IsByColor);
            Assert.Equal(40, document.Database.LayerTable["DOOR"].Color.ColorIndex);
        }

        [Fact]
        public void TheListsStartWithTheLayersOfTheDrawing_ThoseOfExternalReferencesLeftOut()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);

            Assert.Equal(new[] { "0", "KEEP", "OLD-DOOR", "OLD-WALL" }, request.FromLayers.Select(l => l.Name));
            Assert.Equal(request.FromLayers.Select(l => l.Name), request.ToLayers.Select(l => l.Name));
            Assert.Empty(request.Mappings);
            Assert.True(request.FromLayers.Single(l => l.Name == "OLD-WALL").IsInUse);
            Assert.True(request.Settings.ForceColorByLayer);
            Assert.True(request.Settings.TranslateObjectsInBlocks);
            Assert.True(request.Settings.WriteTransactionLog);
        }

        [Fact]
        public void LoadingAStandardsFile_AddsItsLayers_AGivenNameTakingTheFilesSettings()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);

            Assert.True(request.Load(Standards()));

            Assert.Equal(
                new[] { "0", "DOOR", "KEEP", "OLD-DOOR", "OLD-WALL", "WALL" },
                request.ToLayers.Select(l => l.Name)
            );
            CadTranslateLayer keep = request.ToLayers.Single(l => l.Name == "KEEP");
            Assert.Equal(6, keep.Color.ColorIndex);
            CadTranslateLayer wall = request.ToLayers.Single(l => l.Name == "WALL");
            Assert.Equal("DASHED", wall.LinetypeName);
            Assert.Equal(new[] { Path.Combine(_folder, "office.dxf") }, request.LoadedFiles);
        }

        [Fact]
        public void AFileThatCannotBeRead_ChangesNothing_AndSaysWhy()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            string bad = Path.Combine(_folder, "broken.dwg");
            File.WriteAllText(bad, "this is not a drawing");

            Assert.False(request.Load(bad));
            Assert.StartsWith("Cannot read ", request.Message);
            Assert.False(request.Load(Path.Combine(_folder, "missing.dxf")));
            Assert.Equal(4, request.ToLayers.Count);
        }

        [Fact]
        public void MapAndTranslate_MovesTheObjectsOfEachLayer_ThoseInBlocksToo_MakesTheNewLayers_AndDeletesTheOld()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            Assert.Equal(1, request.Map(new[] { "OLD-WALL" }, "WALL"));
            Assert.Equal(1, request.Map(new[] { "old-door" }, "DOOR"));

            CadEditResult result = request.Translate();

            Assert.True(result.Succeeded, result.Message);
            DxfDatabase database = document.Database;
            Assert.False(database.LayerTable.Has("OLD-WALL"));
            Assert.False(database.LayerTable.Has("OLD-DOOR"));
            Assert.Equal("WALL", document.FindEntity("A1").Layer);
            Assert.Equal("DOOR", document.FindEntity("A2").Layer);
            Assert.Equal("KEEP", document.FindEntity("A3").Layer);
            Assert.Equal(new[] { "WALL", "KEEP" }, database.BlockTable["BLK"].Select(e => e.Layer));

            DxfLayerTableRecord wall = database.LayerTable["WALL"];
            Assert.Equal(3, wall.Color.ColorIndex);
            Assert.Equal("DASHED", wall.LinetypeName);
            Assert.True(database.LinetypeTable.Has("DASHED"));
            Assert.Equal(12.0, database.LinetypeTable["DASHED"].DashLengthAt(0), 6);
            Assert.Equal(4, database.LayerTable["DOOR"].Color.ColorIndex);
        }

        [Fact]
        public void TheObjectsOfTheTranslatedLayersAreForcedToByLayer_UnlessTheSettingsSayNo()
        {
            CadDocument forced = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(forced);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Translate();
            Assert.True(forced.FindEntity("A1").Color.IsByLayer);
            Assert.Equal("ByLayer", forced.FindEntity("A1").Linetype, ignoreCase: true);
            Assert.True(forced.Database.BlockTable["BLK"].First().Color.IsByLayer);

            CadDocument free = CadDocument.From(Plan(), "plan.dxf");
            request = Request(free);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Settings.ForceColorByLayer = false;
            request.Settings.ForceLinetypeByLayer = false;
            request.Settings.ForceTransparencyByLayer = false;
            request.Translate();
            Assert.Equal(30, free.FindEntity("A1").Color.ColorIndex);
            Assert.Equal("HIDDEN", free.FindEntity("A1").Linetype, ignoreCase: true);
            Assert.Equal("WALL", free.FindEntity("A1").Layer);
        }

        [Fact]
        public void WithTheObjectsInBlocksLeftAlone_TheBlockKeepsItsLayer_AndSoDoesTheLayerThatHoldsIt()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Settings.TranslateObjectsInBlocks = false;

            Assert.True(request.Translate().Succeeded);

            Assert.Equal("WALL", document.FindEntity("A1").Layer);
            Assert.Equal(new[] { "OLD-WALL", "KEEP" }, document.Database.BlockTable["BLK"].Select(e => e.Layer));
            Assert.True(document.Database.LayerTable.Has("OLD-WALL"));
            Assert.Contains(request.Log, l => l.Contains("OLD-WALL is kept: objects in blocks are still on it."));
        }

        [Fact]
        public void MapSame_GivesALayerOfTheDrawingTheSettingsOfTheStandardsLayerOfItsName()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());

            Assert.Equal(4, request.MapSame());
            Assert.Equal(new[] { "0", "KEEP", "OLD-DOOR", "OLD-WALL" }, request.Mappings.Select(m => m.From));
            Assert.True(request.Translate().Succeeded);

            Assert.Equal(6, document.Database.LayerTable["KEEP"].Color.ColorIndex);
            Assert.Equal("KEEP", document.FindEntity("A3").Layer);
            Assert.True(document.Database.LayerTable.Has("OLD-WALL"));
        }

        [Fact]
        public void Edit_ChangesTheSettingsOneMappingGives_NotTheFilesLayer()
        {
            CadDocument document = CadDocument.From(ModernPlan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Map(new[] { "OLD-DOOR" }, "DOOR");

            Assert.Equal(
                1,
                request.Edit(
                    new[] { "OLD-WALL" },
                    new CadTranslateLayer("ignored")
                    {
                        Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 40),
                        LinetypeName = "HIDDEN",
                        LineWeight = DxfLineWeight.LineWeight035,
                        TransparencyPercent = 50,
                    }
                )
            );
            Assert.Equal(3, request.ToLayers.Single(l => l.Name == "WALL").Color.ColorIndex);

            CadEditResult translated = request.Translate();
            Assert.True(translated.Succeeded, translated.Message);

            DxfLayerTableRecord wall = document.Database.LayerTable["WALL"];
            Assert.Equal(40, wall.Color.ColorIndex);
            Assert.Equal("HIDDEN", wall.LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight035, wall.LineWeight);
            Assert.Equal(50, CadLayerEditor.TransparencyPercent(wall.Transparency));
            Assert.Equal(4, document.Database.LayerTable["DOOR"].Color.ColorIndex);
        }

        [Fact]
        public void ANewLayer_IsMappedTo_AndMadeInTheDrawing()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);

            Assert.False(request.NewLayer(new CadTranslateLayer("A*B")));
            Assert.Equal("\"A*B\" is an invalid layer name.", request.Message);
            Assert.False(request.NewLayer(new CadTranslateLayer("keep")));
            Assert.True(
                request.NewLayer(
                    new CadTranslateLayer("A-ANNO") { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2) }
                )
            );
            Assert.Equal(2, request.Map(new[] { "OLD-DOOR", "OLD-WALL" }, "A-ANNO"));

            Assert.True(request.Translate().Succeeded);

            Assert.Equal("A-ANNO", document.FindEntity("A1").Layer);
            Assert.Equal("A-ANNO", document.FindEntity("A2").Layer);
            Assert.Equal(2, document.Database.LayerTable["A-ANNO"].Color.ColorIndex);
        }

        [Fact]
        public void LayerZeroIsNotMappedToAnotherLayer_AndAMappingToAMissingLayerIsRefused()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);

            Assert.Equal(0, request.Map(new[] { "0" }, "KEEP"));
            Assert.Contains("layer 0 and Defpoints", request.Message);
            Assert.Equal(0, request.Map(new[] { "OLD-WALL" }, "NOWHERE"));
            Assert.Equal("Layer \"NOWHERE\" is not in the Translate To list.", request.Message);
            Assert.Empty(request.Mappings);
            Assert.False(request.Translate().Succeeded);
            Assert.Equal("No layer is mapped.", request.Message);
        }

        [Fact]
        public void TheCurrentLayerTranslatedAway_IsReplacedByTheLayerItWentTo()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            Assert.True(CadLayerEditor.SetCurrent(document, "OLD-DOOR").Succeeded);
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-DOOR" }, "DOOR");

            Assert.True(request.Translate().Succeeded);

            Assert.Equal("DOOR", document.Database.Clayer);
            Assert.False(document.Database.LayerTable.Has("OLD-DOOR"));
        }

        [Fact]
        public void ALayerMappedToAnotherMappedLayer_KeepsTheObjectsOfBothOnTheirOwnTargets()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Map(new[] { "OLD-WALL" }, "OLD-DOOR");
            request.Map(new[] { "OLD-DOOR" }, "KEEP");

            Assert.True(request.Translate().Succeeded);

            Assert.Equal("OLD-DOOR", document.FindEntity("A1").Layer);
            Assert.Equal("KEEP", document.FindEntity("A2").Layer);
            Assert.True(document.Database.LayerTable.Has("OLD-DOOR"));
            Assert.False(document.Database.LayerTable.Has("OLD-WALL"));
        }

        [Fact]
        public void TheCommand_TranslatesOnOk_InOneUndoStep_AndCancelChangesNothing()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            string start = document.Database.ToText();
            string standards = Standards();
            CadCommandSession cancelled = Session(document);
            cancelled.UiRequested += (sender, e) =>
            {
                var request = (CadLayerTranslatorRequest)e.Payload;
                request.Load(standards);
                request.Map(new[] { "OLD-WALL" }, "WALL");
                request.ShowContents(new[] { "OLD-WALL" });
                Assert.Equal(
                    new[] { "0", "KEEP", "OLD-DOOR" },
                    document.Database.LayerTable.Where(l => l.IsOff).Select(l => l.Name).OrderBy(n => n)
                );
                e.Handled = true;
            };
            Run(cancelled, "LAYTRANS");
            Assert.True(document.Database.LayerTable.All(l => !l.IsOff));
            Assert.True(document.Database.LayerTable.Has("OLD-WALL"));

            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                var request = (CadLayerTranslatorRequest)e.Payload;
                request.Load(standards);
                request.Map(new[] { "OLD-WALL", "OLD-DOOR" }, "WALL");
                request.Action = CadDialogAction.Ok;
                e.Handled = true;
            };

            Run(session, "LAYTRANS");

            Assert.Equal("2 layers translated.", session.History.Last());
            Assert.Equal("WALL", document.FindEntity("A2").Layer);
            Assert.True(document.Database.LayerTable.All(l => !l.IsOff));
            Assert.Equal("LAYTRANS", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void WithNoWindow_TheCommandSaysItIsTheDialogOfTheWindow()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadCommandSession session = Run(Session(document), "LAYTRANS");

            Assert.Contains("Layer Translator dialog", session.History.Last());
        }

        [Fact]
        public void SaveWritesTheToListAsAStandardsDwg_WithTheMappings_AndLoadReadsThemBack()
        {
            CadDocument document = CadDocument.From(ModernPlan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            request.Edit(new[] { "OLD-WALL" }, new CadTranslateLayer("x") { Color = DxfColor.FromRgb(10, 20, 30) });
            request.Map(new[] { "OLD-DOOR" }, "DOOR");
            string saved = Path.Combine(_folder, "office.dws");

            Assert.True(request.Save(saved), request.Message);

            Assert.True(CadDrawingFile.IsDwg(saved));
            DxfDatabase file = CadDrawingFile.Open(saved);
            Assert.Equal(3, file.LayerTable["WALL"].Color.ColorIndex);
            Assert.True(file.LinetypeTable.Has("DASHED"));

            CadDocument other = CadDocument.From(ModernPlan(), "plan.dxf");
            CadLayerTranslatorRequest again = Request(other);
            Assert.True(again.Load(saved));
            Assert.Equal(new[] { "OLD-DOOR", "OLD-WALL" }, again.Mappings.Select(m => m.From));
            CadLayerMapping wall = again.Mappings.Single(m => m.From == "OLD-WALL");
            Assert.Equal("WALL", wall.To.Name);
            Assert.True(wall.To.Color.IsByColor);
            Assert.Equal(20, wall.To.Color.Green);

            CadEditResult translated = again.Translate();
            Assert.True(translated.Succeeded, translated.Message);
            Assert.Equal("WALL", other.FindEntity("A1").Layer);
            Assert.Equal("DOOR", other.FindEntity("A2").Layer);
        }

        [Fact]
        public void AStandardsFileSavedAsDxf_IsAlsoLoaded()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");
            string saved = Path.Combine(_folder, "office-standards.dxf");

            Assert.True(request.Save(saved), request.Message);
            Assert.False(CadDrawingFile.IsDwg(saved));
            CadLayerTranslatorRequest again = Request(CadDocument.From(Plan(), "plan.dxf"));
            Assert.True(again.Load(saved));
            Assert.Equal(new[] { "OLD-WALL" }, again.Mappings.Select(m => m.From));
        }

        [Fact]
        public void TheTransactionLog_IsWrittenBesideTheDrawing_WhenTheSettingsAskForIt()
        {
            string path = Path.Combine(_folder, "plan.dxf");
            Plan().Save(path);
            CadDocument document = CadDocument.Open(path);
            CadLayerTranslatorRequest request = Request(document);
            request.Load(Standards());
            request.Map(new[] { "OLD-WALL" }, "WALL");

            Assert.True(request.Translate().Succeeded);

            Assert.Equal(Path.Combine(_folder, "plan.log"), request.LogFile);
            string[] lines = File.ReadAllLines(request.LogFile);
            Assert.Equal("Layer Translator transaction log", lines[0]);
            Assert.Contains(lines, l => l.StartsWith("Layer OLD-WALL translated to layer WALL: 2 objects."));
            Assert.Contains(lines, l => l == "Layer WALL created.");
            Assert.Contains(lines, l => l == "Layer OLD-WALL deleted.");

            string other = Path.Combine(_folder, "second.dxf");
            Plan().Save(other);
            CadDocument second = CadDocument.Open(other);
            CadLayerTranslatorRequest quiet = Request(second);
            quiet.Load(Standards());
            quiet.Map(new[] { "OLD-WALL" }, "WALL");
            quiet.Settings.WriteTransactionLog = false;
            quiet.Translate();
            Assert.Null(quiet.LogFile);
            Assert.False(File.Exists(Path.Combine(_folder, "second.log")));
        }

        [Fact]
        public void ShowLayerContents_ShowsTheLayersSelectedAlone_AndHideContentsGivesThemBack()
        {
            CadDocument document = CadDocument.From(Plan(), "plan.dxf");
            CadLayerTranslatorRequest request = Request(document);
            request.ShowContents(new[] { "OLD-WALL", "OLD-DOOR" });
            Assert.Equal(
                new[] { "0", "KEEP" },
                document.Database.LayerTable.Where(l => l.IsOff).Select(l => l.Name).OrderBy(n => n)
            );

            request.HideContents();
            Assert.True(document.Database.LayerTable.All(l => !l.IsOff));

            request.Settings.ShowLayerContents = false;
            request.ShowContents(new[] { "OLD-WALL" });
            Assert.True(document.Database.LayerTable.All(l => !l.IsOff));
        }

        [Fact]
        public void TranslateIsRegistered()
        {
            Assert.NotNull(CadCommandRegistry.CreateDefault().Find("LAYTRANS"));
        }
    }
}
