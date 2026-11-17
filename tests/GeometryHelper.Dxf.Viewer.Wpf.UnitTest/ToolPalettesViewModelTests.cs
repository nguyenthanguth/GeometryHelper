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
    /// The tool palettes driven with no window through their view models: the tabs and the tools of the one chosen, running a
    /// tool, the menus' cut, copy, paste, delete and rename, how a palette is listed, import and export, what is dropped on a
    /// palette and what is dropped on the canvas, the palettes kept in their file, and the Tool Properties and name dialogs.
    /// </summary>
    public class ToolPalettesViewModelTests
    {
        /// <summary>
        /// A folder holding library.dxf (the block TREE, a circle of 1 500 on ROAD) and plan.dxf, opened in the presenter: a
        /// square 100 by 100 of four lines, and the block BOX of a line. The settings, so the palettes' file, are the folder's.
        /// </summary>
        private static MainViewModel Site(TestFolder folder, out string library)
        {
            DxfDatabase source = CadTemplate.CreateMetric();
            source.LayerTable.Add(new DxfLayerTableRecord { Name = "ROAD", ColorIndex = 3 });
            var tree = new DxfBlockTableRecord { Name = "TREE", Origin = GeoPoint3.Origin };
            source.BlockTable.Add(tree);
            tree.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 1500.0) { Layer = "ROAD" });
            var bush = new DxfBlockTableRecord { Name = "BUSH", Origin = GeoPoint3.Origin };
            source.BlockTable.Add(bush);
            bush.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 400.0));
            library = folder.Write("library.dxf", source);

            DxfDatabase plan = CadTemplate.CreateMetric();
            GeoPoint3[] corner =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 100, 0),
                new GeoPoint3(0, 100, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                plan.ModelSpace.AppendEntity(new DxfLine(corner[i], corner[(i + 1) % 4]));
            }

            var box = new DxfBlockTableRecord { Name = "BOX", Origin = GeoPoint3.Origin };
            plan.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(GeoPoint3.Origin, new GeoPoint3(10, 0, 0)));
            var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
            Assert.True(model.Open(folder.Write("plan.dxf", plan)));
            return model;
        }

        private static ToolItemViewModel Item(ToolPalettesViewModel palettes, string name) =>
            palettes.Items.Single(i => i.Name == name);

        [Fact]
        public void TheViewModel_ListsAPaletteAsATab_AndTheToolsOfTheOneChosen_StartingWithTheStarterSet()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);

                Assert.Equal(
                    new[] { "Draw", "Modify", "Annotation", "Hatches", "Architectural Hatches", "ISO Hatches" },
                    palettes.Tabs.Select(t => t.Name)
                );
                Assert.Equal(new[] { true, false, false, false, false, false }, palettes.Tabs.Select(t => t.IsActive));
                Assert.Equal("Line", palettes.Items[0].Name);
                Assert.Equal(8, palettes.Items.Count);
                Assert.False(palettes.IsEmpty);
                Assert.False(palettes.IsList);
                Assert.False(palettes.ShowsNames);
                Assert.Equal(44.0, palettes.CellWidth);
                Assert.Equal(44.0, palettes.CellHeight);

                palettes.Choose(palettes.Tabs[3]);

                Assert.True(palettes.Tabs[3].IsActive);
                Assert.False(palettes.Tabs[0].IsActive);
                Assert.Equal("Hatches", palettes.Active.Name);
                Assert.Equal("ANSI", palettes.Items[0].Name);
                Assert.True(palettes.Items[0].IsText);
                Assert.True(palettes.Items[9].IsSeparator);
                Assert.Equal(3, model.ToolPalettes.ActiveIndex);
                Assert.Equal(Path.Combine(folder.FullPath, "ToolPalettes.json"), model.ToolPalettesPath);
            }
        }

        [Fact]
        public void AToolIsRunByAClick_ACommandToolStartsItsCommand_ATextOrSeparatorDoesNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);

                Assert.True(palettes.Run(Item(palettes, "Circle")));

                Assert.Equal("CIRCLE", model.Session.Current.Name);
                Assert.Equal("Circle started.", palettes.Message);
                model.Execute("50,50");
                model.Execute("20");
                Assert.Single(model.Document.Database.ModelSpace.OfType<DxfCircle>());
                palettes.Choose(palettes.Tabs[3]);
                Assert.False(palettes.Run(palettes.Items[0]));
                Assert.False(palettes.Run(palettes.Items[9]));
                Assert.False(palettes.Run(null));
                Assert.False(model.Session.IsRunning);
            }
        }

        [Fact]
        public void AHatchToolClicked_AsksForAPoint_AndHatchesTheAreaWithItsPatternAndScale()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);
                palettes.Choose(palettes.Tabs[3]);
                ToolItemViewModel steel = Item(palettes, "ANSI32");
                steel.Tool.Scale = 2.0;
                steel.Tool.Angle = 30.0;

                Assert.True(palettes.Run(steel));
                Assert.Equal("HATCH", model.Session.Current.Name);
                model.Execute("50,50");
                model.Enter();

                DxfHatch hatch = Assert.Single(model.Document.Database.ModelSpace.OfType<DxfHatch>());
                Assert.Equal("ANSI32", hatch.PatternName);
                Assert.Equal(2.0, hatch.PatternScale, 9);
                Assert.Equal(30.0 * Math.PI / 180.0, hatch.PatternAngle, 9);
            }
        }

        [Fact]
        public void ABlockToolClicked_AsksTheInsertionPointOnTheCommandLine_AndPlacesTheBlock()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palettes = new ToolPalettesViewModel(model);
                ToolPaletteTabViewModel trees = palettes.NewPalette("Trees");
                CadTool tree = CadTool.ForBlock("Tree", library, "TREE");
                tree.Scale = 0.5;
                trees.Palette.Add(tree);
                palettes.Reload();

                Assert.True(palettes.Run(Item(palettes, "Tree")));
                Assert.True(model.Session.IsRunning);
                model.Execute("300,400");

                DxfBlockReference placed = Assert.Single(
                    model.Document.Database.ModelSpace.OfType<DxfBlockReference>()
                );
                Assert.Equal("TREE", placed.Name);
                Assert.Equal(0.5, placed.ScaleFactors.X, 9);
                Assert.Equal(300.0, placed.Position.X, 6);
                Assert.True(model.Document.Database.BlockTable.Has("TREE"));
            }
        }

        [Fact]
        public void APaletteMade_Renamed_AndDeleted_IsKeptInItsFile_AndReadBackByAnotherWindow()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);

                ToolPaletteTabViewModel made = palettes.NewPalette("Site");
                Assert.Equal(7, palettes.Tabs.Count);
                Assert.True(made.IsActive);
                Assert.True(palettes.IsEmpty);
                Assert.Equal(
                    "This palette has no tools: drag objects of the drawing, or blocks of DesignCenter, onto it.",
                    palettes.EmptyText
                );
                Assert.True(File.Exists(model.ToolPalettesPath));
                Assert.True(palettes.RenamePalette(made, "Site plan"));
                Assert.False(palettes.RenamePalette(made, "  "));
                Assert.True(palettes.DeletePalette(palettes.Tabs[1]));
                Assert.False(palettes.DeletePalette(null));

                var again = new MainViewModel { SettingsPath = model.SettingsPath };
                Assert.Equal(
                    new[] { "Draw", "Annotation", "Hatches", "Architectural Hatches", "ISO Hatches", "Site plan" },
                    again.ToolPalettes.Palettes.Select(p => p.Name)
                );
                Assert.Equal(5, again.ToolPalettes.ActiveIndex);
            }
        }

        [Fact]
        public void AFileThatCannotBeWritten_IsSaidOnTheCommandLine_AndThePalettesStayAsTheyAre()
        {
            using (var folder = new TestFolder())
            {
                string blocker = folder.WriteText("blocker", "a file where a folder should be");
                var model = new MainViewModel { SettingsPath = Path.Combine(blocker, "sub", "settings.json") };
                Assert.True(model.Open(folder.Write("plan.dxf", CadTemplate.CreateMetric())));
                var palettes = new ToolPalettesViewModel(model);

                palettes.NewPalette("Site");

                Assert.Equal(7, palettes.Tabs.Count);
                Assert.True(model.ToolPalettes.IsModified);
                Assert.Contains(model.Session.History, l =>
                    l.StartsWith("Cannot save the tool palettes:", StringComparison.Ordinal)
                );
            }
        }

        [Fact]
        public void TextAndSeparatorsAreAddedAfterATool_CutCopyAndPasteMoveCopies_DeleteAndRenameWork()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);
                ToolItemViewModel line = Item(palettes, "Line");

                ToolItemViewModel heading = palettes.AddText("Basics", line);
                ToolItemViewModel rule = palettes.AddSeparator(line);
                palettes.AddText("   ");

                Assert.Equal(new[] { "Line", "", "Basics", "Polyline" }, palettes.Items.Take(4).Select(i => i.Name));
                Assert.True(palettes.Items[1].IsSeparator);
                Assert.Equal("Text", palettes.Items.Last().Name);

                palettes.Copy(new[] { Item(palettes, "Circle"), Item(palettes, "Arc") });
                Assert.True(palettes.CanPaste);
                IReadOnlyList<ToolItemViewModel> pasted = palettes.Paste(Item(palettes, "Line"));
                Assert.Equal(new[] { "Circle", "Arc" }, pasted.Select(i => i.Name));
                Assert.Equal(new[] { "Line", "Circle", "Arc", "" }, palettes.Items.Take(4).Select(i => i.Name));

                palettes.Cut(new[] { Item(palettes, "Point") });
                Assert.DoesNotContain(palettes.Items, i => i.Name == "Point");
                palettes.Choose(palettes.Tabs[1]);
                palettes.Paste();
                Assert.Equal("Point", palettes.Items.Last().Name);
                Assert.NotSame(palettes.Items.Last().Tool, model.ToolPalettes.Clipboard[0]);

                palettes.Rename(Item(palettes, "Point"), "Dot");
                palettes.Rename(Item(palettes, "Dot"), "  ");
                Assert.Equal("Dot", palettes.Items.Last().Name);
                palettes.Rename(palettes.Items.First(), "Move it");
                Assert.Equal("Move it", palettes.Items.First().Name);
                palettes.Delete(new[] { Item(palettes, "Dot"), Item(palettes, "Erase") });
                Assert.DoesNotContain(palettes.Items, i => i.Name == "Dot" || i.Name == "Erase");
                palettes.Delete(null);
                palettes.Cut(Array.Empty<ToolItemViewModel>());
                Assert.True(model.ToolPalettes.IsModified == false);
                Assert.NotNull(heading);
                Assert.NotNull(rule);
            }
        }

        [Fact]
        public void ADraggedToolIsMovedToAnotherPlaceOfItsPalette_BeforeTheOneItIsDroppedOn_OrLast()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);

                palettes.Move(Item(palettes, "Point"), Item(palettes, "Line"));
                Assert.Equal(new[] { "Point", "Line" }, palettes.Items.Take(2).Select(i => i.Name));
                palettes.Move(Item(palettes, "Point"), null);
                Assert.Equal("Point", palettes.Items.Last().Name);
                palettes.Move(Item(palettes, "Line"), Item(palettes, "Line"));
                Assert.Equal("Line", palettes.Items[0].Name);
                Assert.Equal(
                    new[] { "Line", "Polyline", "Circle", "Arc", "Rectangle", "Ellipse", "Spline", "Point" },
                    new CadToolPalettes[] { new MainViewModel { SettingsPath = model.SettingsPath }.ToolPalettes }
                        .Single()
                        .Palettes[0]
                        .Tools.Select(t => t.Name)
                );
            }
        }

        [Fact]
        public void ViewOptions_SetTheStyleAndTheIconSize_ForThePaletteShownOrForAll()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);

                palettes.SetView(CadToolViewStyle.IconWithText, 48, false);

                Assert.Equal(CadToolViewStyle.IconWithText, palettes.ViewStyle);
                Assert.True(palettes.ShowsNames);
                Assert.Equal(48, palettes.IconSize);
                Assert.Equal(88.0, palettes.CellWidth);
                Assert.Equal(80.0, palettes.CellHeight);
                Assert.Equal(CadToolViewStyle.IconOnly, model.ToolPalettes.Palettes[1].ViewStyle);

                palettes.SetView(CadToolViewStyle.List, 500, true);

                Assert.True(palettes.IsList);
                Assert.Equal(CadToolPalette.MaxIconSize, palettes.IconSize);
                Assert.True(double.IsNaN(palettes.CellWidth));
                Assert.All(model.ToolPalettes.Palettes, p => Assert.Equal(CadToolViewStyle.List, p.ViewStyle));
                Assert.True(palettes.Items.All(i => i.UsesRow));
                Assert.Equal(
                    CadToolViewStyle.List,
                    new MainViewModel { SettingsPath = model.SettingsPath }
                        .ToolPalettes
                        .Palettes[3]
                        .ViewStyle
                );
            }
        }

        [Fact]
        public void APaletteExportedAndImported_ComesBackAfterTheLastWithAUniqueName_AndABadFileIsSaid()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);
                string file = Path.Combine(folder.FullPath, "draw.ghtp");

                Assert.True(palettes.Export(file, false));
                Assert.Equal("Draw exported to draw.ghtp.", palettes.Message);
                Assert.True(palettes.Import(file));

                Assert.Equal("1 palette imported from draw.ghtp.", palettes.Message);
                Assert.Equal("Draw (2)", palettes.Tabs.Last().Name);
                Assert.True(palettes.Tabs.Last().IsActive);
                Assert.True(palettes.Export(file, true));
                Assert.True(palettes.Import(file));
                Assert.Equal("7 palettes imported from draw.ghtp.", palettes.Message);
                Assert.Equal(14, palettes.Tabs.Count);

                File.WriteAllText(file, "not palettes");
                Assert.False(palettes.Import(file));
                Assert.StartsWith("Cannot import ", palettes.Message, StringComparison.Ordinal);
                Assert.False(palettes.Import(Path.Combine(folder.FullPath, "none.ghtp")));
                Assert.False(palettes.Export(Path.Combine(folder.FullPath, "plan.dxf", "x", "y.ghtp"), false));
                Assert.StartsWith("Cannot write ", palettes.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ObjectsDroppedOnAPalette_BecomeToolsOfTheirLayerColourAndLinetype_AndWhatNoToolDrawsIsSaid()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                var palettes = new ToolPalettesViewModel(model);
                DxfEntity first = model.Document.Database.ModelSpace.OfType<DxfLine>().First();
                first.Layer = "WALLS";
                var circle = new DxfCircle(new GeoPoint3(5, 5, 0), new GeoVector3(0, 0, 1), 3.0);
                model.Document.Database.ModelSpace.AppendEntity(circle);
                var viewport = new DxfRay();
                model.Document.Database.ModelSpace.AppendEntity(viewport);
                palettes.NewPalette("Mine");

                bool added = palettes.Drop(
                    ObjectDrag.Format,
                    ObjectDrag.Write(new[] { first.Handle, circle.Handle, viewport.Handle })
                );

                Assert.True(added);
                Assert.Equal(new[] { "Line", "Circle" }, palettes.Items.Select(i => i.Name));
                Assert.Equal("LINE", palettes.Items[0].Tool.Command);
                Assert.Equal("WALLS", palettes.Items[0].Tool.Layer);
                Assert.Equal("2 tools added to Mine.", palettes.Message);
                Assert.Contains(model.Session.History, l => l.Contains("1 not drawn by any tool."));
                Assert.False(palettes.Drop(ObjectDrag.Format, ObjectDrag.Write(new[] { "NOSUCH" })));
                Assert.False(palettes.Drop("something else", "x"));
                Assert.Equal(
                    2,
                    new MainViewModel { SettingsPath = model.SettingsPath }
                        .ToolPalettes.Palettes.Last()
                        .Tools.Count
                );
            }
        }

        [Fact]
        public void BlocksAndDrawingsDroppedFromDesignCenter_BecomeBlockToolsOfThatDrawing_BeforeTheToolDroppedOn()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palettes = new ToolPalettesViewModel(model);
                palettes.NewPalette("Trees");
                palettes.AddText("Existing");

                Assert.True(
                    palettes.Drop(
                        DesignCenterDrag.Format,
                        DesignCenterDrag.Write(library, CadDesignCenterKind.Blocks, new[] { "TREE", "BUSH" }),
                        palettes.Items[0]
                    )
                );
                Assert.True(
                    palettes.Drop(DesignCenterDrag.Format, DesignCenterDrag.Write(library, null, new string[0]))
                );
                Assert.False(
                    palettes.Drop(
                        DesignCenterDrag.Format,
                        DesignCenterDrag.Write(library, CadDesignCenterKind.Layers, new[] { "ROAD" })
                    )
                );

                Assert.Equal(new[] { "TREE", "BUSH", "Existing", "library" }, palettes.Items.Select(i => i.Name));
                Assert.Equal(library, palettes.Items[0].Tool.File);
                Assert.Equal("TREE", palettes.Items[0].Tool.BlockName);
                Assert.Equal(string.Empty, palettes.Items[3].Tool.BlockName);
                Assert.Equal(CadToolKind.Block, palettes.Items[3].Kind);
            }
        }

        [Fact]
        public void DesignCentersMenu_OffersCreateToolPalette_ForADrawingItsBlocksAndSomeBlocks()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var designCenter = new DesignCenterViewModel(model, new[] { folder.FullPath });
                Assert.True(designCenter.Load(library));

                DesignCenterItemViewModel blocks = designCenter.Items.Single(i => i.Name == "Blocks");
                DesignCenterAction ofBlocks = designCenter.ToolPaletteActionFor(new[] { blocks });
                Assert.Equal("Create Tool Palette of Blocks", ofBlocks.Text);
                ofBlocks.Run();

                CadToolPalette palette = model.ToolPalettes.Palettes.Last();
                Assert.Equal("library", palette.Name);
                Assert.Equal(new[] { "BUSH", "TREE" }, palette.Tools.Select(t => t.Name));
                Assert.All(palette.Tools, t => Assert.Equal(library, t.File));
                Assert.Contains(model.Session.History, l => l == "Tool palette library made of 2 blocks.");

                designCenter.NavigateTo(library, CadDesignCenterKind.Blocks);
                DesignCenterItemViewModel tree = designCenter.Items.Single(i => i.Name == "TREE");
                DesignCenterAction ofTree = designCenter.ToolPaletteActionFor(new[] { tree });
                Assert.Equal("Create Tool Palette", ofTree.Text);
                ofTree.Run();

                Assert.Equal("library (2)", model.ToolPalettes.Palettes.Last().Name);
                Assert.Equal(new[] { "TREE" }, model.ToolPalettes.Palettes.Last().Tools.Select(t => t.Name));
                Assert.Equal(
                    new[] { "TREE" },
                    new MainViewModel { SettingsPath = model.SettingsPath }
                        .ToolPalettes.Palettes.Last()
                        .Tools.Select(t => t.Name)
                );

                designCenter.NavigateTo(folder.FullPath, null);
                DesignCenterItemViewModel drawing = designCenter.Items.Single(i => i.Name == "library.dxf");
                Assert.Equal(
                    "Create Tool Palette of Blocks",
                    designCenter.ToolPaletteActionFor(new[] { drawing }).Text
                );
                Assert.Null(designCenter.ToolPaletteActionFor(Array.Empty<DesignCenterItemViewModel>()));
                Assert.Null(designCenter.ToolPaletteActionFor(null));
                Assert.Null(model.CreateToolPaletteOfBlocks(null));
                Assert.Null(model.CreateToolPaletteOfBlocks(Path.Combine(folder.FullPath, "none.dxf")));
                Assert.Contains(model.Session.History, l => l.StartsWith("Cannot read ", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void ABlockOrHatchToolDroppedOnTheCanvas_IsPlacedOrHatchedThere_ACommandToolIsNotDropped()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                CadToolPalette made = model.ToolPalettes.Add("Mixed");
                CadTool box = CadTool.ForBlock("Box", null, "BOX");
                box.Scale = 3.0;
                made.Add(box);
                made.Add(CadTool.ForHatch("Solid", "SOLID"));
                made.Add(CadTool.ForCommand("Line", "LINE"));
                int palette = model.ToolPalettes.Palettes.Count - 1;

                Assert.True(model.DropToolPaletteTool(ToolPaletteDrag.Write(palette, 0), new GeoPoint2(40, 60)));
                DxfBlockReference placed = Assert.Single(
                    model.Document.Database.ModelSpace.OfType<DxfBlockReference>()
                );
                Assert.Equal(40.0, placed.Position.X, 9);
                Assert.Equal(60.0, placed.Position.Y, 9);
                Assert.Equal(3.0, placed.ScaleFactors.X, 9);
                Assert.True(model.DropToolPaletteTool(ToolPaletteDrag.Write(palette, 1), new GeoPoint2(20, 20)));
                Assert.True(Assert.Single(model.Document.Database.ModelSpace.OfType<DxfHatch>()).IsSolidFill);
                Assert.False(model.DropToolPaletteTool(ToolPaletteDrag.Write(palette, 2), new GeoPoint2(1, 1)));
                Assert.False(model.DropToolPaletteTool(ToolPaletteDrag.Write(palette, 9), new GeoPoint2(1, 1)));
                Assert.False(model.DropToolPaletteTool(ToolPaletteDrag.Write(-1, 0), new GeoPoint2(1, 1)));
                Assert.False(model.DropToolPaletteTool("nonsense", new GeoPoint2(1, 1)));
                Assert.False(model.Session.IsRunning);
            }
        }

        [Fact]
        public void ADragOfATool_IsWrittenAsTwoPlaces_AndReadBack()
        {
            Assert.True(ToolPaletteDrag.TryRead(ToolPaletteDrag.Write(3, 12), out int palette, out int tool));
            Assert.Equal(3, palette);
            Assert.Equal(12, tool);
            Assert.False(ToolPaletteDrag.TryRead("3", out _, out _));
            Assert.False(ToolPaletteDrag.TryRead("a\nb", out _, out _));
            Assert.False(ToolPaletteDrag.TryRead(null, out _, out _));
            Assert.Equal(new[] { "A1", "B2" }, ObjectDrag.Read(ObjectDrag.Write(new[] { "A1", "B2" })));
            Assert.Empty(ObjectDrag.Read(null));
        }

        [Fact]
        public void TheCommandsAskTheWindow_TheAliasTpToo_AndAKeyGivenToolpalettesShowsThemOrClosesThem()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                ToolPalettesViewModel shown = null;
                int closed = 0;
                model.ShowToolPalettes = p => shown = p;
                model.CloseToolPalettes = () =>
                {
                    closed++;
                    model.NoteToolPalettesShown(false);
                };

                model.Execute("TOOLPALETTES");

                Assert.NotNull(shown);
                Assert.Equal(6, shown.Tabs.Count);
                shown = null;
                model.Execute("TP");
                Assert.NotNull(shown);
                model.NoteToolPalettesShown(true);
                Assert.True(model.IsToolPalettesShown);
                Assert.True(model.ToggleToolPalettes());
                Assert.Equal(1, closed);
                Assert.False(model.IsToolPalettesShown);
                shown = null;
                Assert.True(model.ToggleToolPalettes());
                Assert.NotNull(shown);
                model.Execute("TOOLPALETTESCLOSE");
                Assert.Equal(2, closed);
            }
        }

        [Fact]
        public void AKeyGivenToolpalettes_ShowsThePalettesWhenClosed_AndClosesThemWhenOpen_AndNoKeyIsGivenThemAtFirst()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                int shown = 0;
                int closed = 0;
                model.ShowToolPalettes = p =>
                {
                    shown++;
                    model.NoteToolPalettesShown(true);
                };
                model.CloseToolPalettes = () =>
                {
                    closed++;
                    model.NoteToolPalettesShown(false);
                };

                // Ctrl+3 is the ribbon's, as the presenter has it: the palettes have no key until one is given them.
                Assert.DoesNotContain("TOOLPALETTES", CadShortcuts.Default.Keys.Values);
                Assert.Equal("RIBBON", CadShortcuts.Default.Keys["Ctrl+3"]);
                Dictionary<string, string> keys = CadShortcuts.Default.Keys.ToDictionary(p => p.Key, p => p.Value);
                keys["Ctrl+Alt+3"] = "TOOLPALETTES";
                model.Session.Registry.Shortcuts = new CadShortcuts(CadShortcuts.Default.Aliases, keys);

                Assert.True(model.RunKey("Ctrl+Alt+3"));
                Assert.Equal(1, shown);
                Assert.True(model.IsToolPalettesShown);
                Assert.True(model.RunKey("Ctrl+Alt+3"));
                Assert.Equal(1, closed);
                Assert.False(model.IsToolPalettesShown);
            }
        }

        [Fact]
        public void WithNoWindowOrNoDrawing_TheCommandsAreSaid_AndAKeyDoesNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);

                model.Execute("TOOLPALETTES");
                Assert.Equal(
                    "The tool palettes are a palette of the window; there is none to show.",
                    model.Session.History.Last()
                );
                Assert.False(model.ToggleToolPalettes());
                Assert.False(new MainViewModel().ToggleToolPalettes());
                Assert.False(new MainViewModel().RunTool(CadTool.ForCommand("Line", "LINE")));
                Assert.Throws<ArgumentNullException>(() => model.RunTool(null));
            }
        }

        [Fact]
        public void TheAliasTpAndTheCloseCommandAreInTheRegistry()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out _);
                Assert.Equal("TOOLPALETTES", model.Session.Registry.Find("TP").Name);
                Assert.NotNull(model.Session.Registry.Find("TOOLPALETTESCLOSE"));
            }
        }

        [Fact]
        public void TheItemOfATool_GivesItsIconKeyTooltipAndPicture()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Site(folder, out string library);
                var palettes = new ToolPalettesViewModel(model);
                ToolItemViewModel line = Item(palettes, "Line");

                Assert.Equal("GhIconLine", line.IconKey);
                Assert.Equal("Line\nDraws straight line segments\nCommand: LINE", line.Tooltip);
                Assert.Null(line.Picture);
                Assert.True(line.IsTool);
                CadTool odd = CadTool.ForCommand("Odd", "LINE");
                model.ToolPalettes.Active.Add(odd);
                palettes.Reload();
                Assert.Equal("GhIconPluginCommand", Item(palettes, "Odd").IconKey);

                palettes.Choose(palettes.Tabs[3]);
                ToolItemViewModel hatch = Item(palettes, "ANSI32");
                Assert.Equal("GhIconHatch", hatch.IconKey);
                Assert.NotNull(hatch.Picture);
                Assert.Equal("ANSI32\nANSI Steel\nHatch: ANSI32", hatch.Tooltip);

                ToolPaletteTabViewModel trees = palettes.NewPalette("Trees");
                trees.Palette.Add(CadTool.ForBlock("Tree", library, "TREE"));
                trees.Palette.Add(CadTool.ForBlock("Box", null, "BOX"));
                trees.Palette.Add(CadTool.ForBlock("Whole", library, string.Empty));
                trees.Palette.Add(CadTool.ForBlock("Ghost", Path.Combine(folder.FullPath, "none.dxf"), "GHOST"));
                palettes.Reload();

                Assert.NotNull(Item(palettes, "Tree").Picture);
                Assert.NotNull(Item(palettes, "Box").Picture);
                Assert.NotNull(Item(palettes, "Whole").Picture);
                Assert.Null(Item(palettes, "Ghost").Picture);
                Assert.Equal("GhIconInsertBlock", Item(palettes, "Tree").IconKey);
                Assert.Equal("Tree\nBlock: TREE\nFrom: " + library, Item(palettes, "Tree").Tooltip);
                Assert.Equal("Whole\nBlock: the whole drawing\nFrom: " + library, Item(palettes, "Whole").Tooltip);
            }
        }

        [Fact]
        public void ToolProperties_ShowTheToolsOwnFields_ApplyWritesThemAndCancelLeavesTheToolAsItWas()
        {
            CadTool block = CadTool.ForBlock("Tree", @"C:\lib\trees.dwg", "TREE");
            block.Layer = "TREES";
            var layers = new[] { "0", "WALL" };
            var linetypes = new[] { "Continuous", "DASHED", "ByLayer" };

            var properties = new ToolPropertiesViewModel(block, layers, linetypes);

            Assert.Equal("Tool Properties", properties.Title);
            Assert.True(properties.IsBlock && properties.HasOverrides);
            Assert.False(properties.IsHatch || properties.IsCommand);
            Assert.Equal(new[] { "(Current)", "0", "WALL", "TREES" }, properties.Layers);
            Assert.Equal(new[] { "(Current)", "ByLayer", "ByBlock", "Continuous", "DASHED" }, properties.Linetypes);
            Assert.Equal("TREES", properties.Layer);
            Assert.Equal("(Current)", properties.Color);
            Assert.Equal("(Current)", properties.LineWeight);
            Assert.Equal("1", properties.Scale);
            Assert.Equal("0", properties.Angle);

            properties.Name = "Oak";
            properties.Scale = "2.5";
            properties.Angle = "45";
            properties.Color = "red";
            properties.Linetype = "DASHED";
            properties.LineWeight = "0.35 mm";
            properties.Layer = "(Current)";
            properties.Explode = true;
            properties.PromptForScale = true;
            Assert.Equal("Tree", block.Name);
            Assert.Equal("TREES", block.Layer);
            Assert.True(properties.Apply());

            Assert.Equal("Oak", block.Name);
            Assert.Equal(2.5, block.Scale);
            Assert.Equal(45.0, block.Angle);
            Assert.Equal("red", block.Color);
            Assert.Equal("DASHED", block.Linetype);
            Assert.Equal(35, block.LineWeight);
            Assert.Null(block.Layer);
            Assert.True(block.Explode && block.PromptForScale);
            Assert.False(block.PromptForRotation);

            properties.LineWeight = "ByBlock";
            properties.Color = "(Current)";
            properties.Linetype = "(Current)";
            Assert.True(properties.Apply());
            Assert.Equal(-2, block.LineWeight);
            Assert.Null(block.Color);
            Assert.Null(block.Linetype);
        }

        [Fact]
        public void ToolProperties_RefuseWhatCannotWork_SayWhy_AndWriteNothing()
        {
            CadTool block = CadTool.ForBlock("Tree", null, "TREE");
            var properties = new ToolPropertiesViewModel(block, null, null);
            var seen = new List<string>();
            properties.PropertyChanged += (s, e) => seen.Add(e.PropertyName);

            properties.Name = " ";
            Assert.Equal("The tool needs a name.", properties.Error);
            Assert.False(properties.CanOk);
            Assert.False(properties.Apply());
            properties.Name = "Tree";
            properties.Scale = "0";
            Assert.Equal("The scale is a number above nought.", properties.Error);
            properties.Scale = "1";
            properties.Angle = "x";
            Assert.Equal("The rotation is a number of degrees.", properties.Error);
            properties.Angle = "0";
            properties.BlockName = "";
            Assert.Equal("A block tool needs a block or a drawing.", properties.Error);
            properties.SourceFile = @"C:\x.dwg";
            Assert.Null(properties.Error);
            properties.Color = "chartreuse";
            Assert.NotNull(properties.Error);
            properties.Color = "300";
            Assert.Equal("A colour's index is 1 to 255.", properties.Error);
            properties.Color = "255,0,0";
            Assert.Equal("The colour is ByLayer, ByBlock or 1 to 255.", properties.Error);
            Assert.False(properties.Apply());
            Assert.Equal("TREE", block.BlockName);
            Assert.Contains(nameof(ToolPropertiesViewModel.Error), seen);
            Assert.Contains(nameof(ToolPropertiesViewModel.CanOk), seen);

            var command = new ToolPropertiesViewModel(CadTool.ForCommand("Line", "LINE"), null, null);
            command.Command = "  ^C^C_ ";
            Assert.Equal("The command line is empty.", command.Error);
            command.Command = "LINE 0,0";
            Assert.True(command.IsCommand);
            Assert.True(command.Apply());

            var text = new ToolPropertiesViewModel(CadTool.ForText("Notes"), null, null);
            Assert.Equal("Text Properties", text.Title);
            Assert.True(text.IsText);
            Assert.False(text.HasOverrides);
            Assert.Equal("Notes", text.Name);
        }

        [Fact]
        public void HatchToolProperties_ShowPatternSpacingOrGradientByType_AndWriteTheHatchOfTheTool()
        {
            CadTool hatch = CadTool.ForHatch("Steel", "ANSI32");
            var properties = new ToolPropertiesViewModel(hatch, null, null);

            Assert.True(properties.IsHatch);
            Assert.True(properties.ShowsPattern && properties.ShowsAngle);
            Assert.False(properties.ShowsSpacing || properties.ShowsGradient);
            Assert.Contains("ANSI31", properties.Patterns);
            Assert.Equal(9, properties.Gradients.Count);

            properties.HatchType = "User defined";
            Assert.True(properties.ShowsSpacing);
            Assert.False(properties.ShowsPattern);
            properties.Spacing = "0";
            Assert.Equal("The spacing is a number above nought.", properties.Error);
            properties.Spacing = "4.5";
            properties.HatchType = "Gradient";
            properties.Gradient = "GR_SPHER";
            Assert.True(properties.ShowsGradient);
            properties.HatchType = "Solid";
            Assert.False(properties.ShowsAngle);
            Assert.Null(properties.Error);
            properties.HatchType = "Pattern";
            properties.Pattern = "AR-SAND";
            properties.Scale = "0.5";
            properties.Angle = "15";
            properties.Associative = false;
            Assert.True(properties.Apply());

            Assert.Equal(CadHatchType.Pattern, hatch.HatchType);
            Assert.Equal("AR-SAND", hatch.Pattern);
            Assert.Equal(0.5, hatch.Scale);
            Assert.Equal(15.0, hatch.Angle);
            Assert.Equal(4.5, hatch.Spacing);
            Assert.Equal("GR_SPHER", hatch.Gradient);
            Assert.False(hatch.Associative);
        }

        [Fact]
        public void TheNameAndViewOptionsDialogs_KeepTheirChoicesInsideTheirLimits()
        {
            var name = new ToolPaletteNameViewModel("Rename Palette", "Palette name:", "Draw");
            Assert.True(name.CanOk);
            name.Name = "   ";
            Assert.False(name.CanOk);
            name.Name = null;
            Assert.Equal(string.Empty, name.Name);
            Assert.Throws<ArgumentNullException>(() => new ToolPaletteNameViewModel(null, "x", "y"));

            var view = new ToolPaletteViewOptionsViewModel(CadToolViewStyle.List, 5);
            Assert.Equal(16, view.IconSize);
            Assert.True(view.IsList);
            view.IsIconWithText = true;
            Assert.Equal(CadToolViewStyle.IconWithText, view.Style);
            Assert.False(view.IsList);
            view.IsIconOnly = false;
            Assert.Equal(CadToolViewStyle.IconWithText, view.Style);
            view.IconSize = 1000;
            Assert.Equal(96, view.IconSize);
            Assert.Equal("96 px", view.IconSizeText);
            Assert.False(view.ApplyToAll);
        }
    }
}
