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
    /// The dialog items of round 4 driven with no window shown: the Viewports dialog's Change view to, Visual Style and Rename,
    /// Select Color and Select Linetype for the elements and fill of a multiline style, the Hatch dialog's Click to set new
    /// origin, Insert Object, the Drawing Recovery Manager, Rename with wildcards, and Check Spelling's Settings and Undo.
    /// </summary>
    public class DialogsSixWindowTests
    {
        /// <summary>A drawing with two model views, ENTRY at (100, 50) 40 high and 80 wide, and NORTH, a text "Helo wrold" and layers S-BEAM, S-COLUMN, WALLS.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "NORTH",
                    CenterPoint = new GeoPoint2(0, 500),
                    Height = 100,
                    Width = 100,
                }
            );
            db.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "ENTRY",
                    CenterPoint = new GeoPoint2(100, 50),
                    Height = 40,
                    Width = 80,
                }
            );
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "S-BEAM" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "S-COLUMN" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "S-BEAM" }
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 50, 0),
                    Height = 5,
                    TextString = "Helo wrold",
                }
            );
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("dialogs6.dxf", Drawing())));
            return model;
        }

        #region Viewports

        [Fact]
        public void Viewports_ChangeViewTo_ListsCurrentAndTheModelViews_AndOkShowsTheViewInTheTilePicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowViewports = dialog =>
                {
                    Assert.Equal(new[] { "*Current*", "ENTRY", "NORTH" }, dialog.Views);
                    dialog.Configuration = "Two: Vertical";
                    Assert.Equal(new[] { "*Current*", "*Current*" }, dialog.PreviewLabels);
                    Assert.Equal(0, dialog.PickedTile);
                    dialog.PickedTile = 1;
                    dialog.ChangeViewTo = "ENTRY";
                    Assert.Equal(new[] { "*Current*", "ENTRY" }, dialog.PreviewLabels);
                    dialog.PickedTile = 0;
                    Assert.Equal("*Current*", dialog.ChangeViewTo);
                    return true;
                };

                model.Execute("VPORTS");

                CadTiledViewports tiles = model.Document.TiledViewports;
                Assert.Equal(2, tiles.Tiles.Count);
                CadTiledViewport entry = tiles.Tiles[1];
                Assert.Equal(100.0, entry.Center.X, 6);
                Assert.Equal(50.0, entry.Center.Y, 6);
                Assert.True(entry.Width >= 80.0 - 1E-9 && entry.Height >= 40.0 - 1E-9);
                Assert.NotEqual(entry.Center, tiles.Tiles[0].Center);
                Assert.Equal("VPORTS", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void Viewports_ChangeViewTo_OnTheActiveConfiguration_ChangesTheViewOfTheTilePicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowViewports = dialog =>
                {
                    Assert.Equal(ViewportsViewModel.ActiveConfiguration, dialog.Configuration);
                    dialog.ChangeViewTo = "NORTH";
                    return true;
                };

                model.Execute("VPORTS");

                CadTiledViewport current = model.Document.TiledViewports.Current;
                Assert.Equal(0.0, current.Center.X, 6);
                Assert.Equal(500.0, current.Center.Y, 6);
            }
        }

        [Fact]
        public void Viewports_VisualStyle_ListsTheStylesOfTheDrawing_2DWireframeAloneWhereItHasNone()
        {
            var dialog = new ViewportsViewModel(CadDocument.From(new DxfDatabase("AC1027"), "vs.dxf"));
            Assert.Equal(new[] { "2D Wireframe" }, dialog.VisualStyles);
            Assert.Equal("2D Wireframe", dialog.VisualStyle);
        }

        [Fact]
        public void Viewports_VisualStyle_NamesAutoCadsStylesAsItsListDoes_AndLeavesTheInternalOnesOut()
        {
            Assert.Equal(
                new[] { "2D Wireframe", "Conceptual", "Hidden", "Shades of Gray", "My style" },
                ViewportsViewModel.VisualStyleNames(
                    new[]
                    {
                        "2dWireframe",
                        "Basic",
                        "Conceptual",
                        "Flat",
                        "Gouraud",
                        "Hidden",
                        "Shades of Gray",
                        "My style",
                    }
                )
            );
        }

        [Fact]
        public void Viewports_NamedViewports_RenameRenamesTheOnePicked_RefusesATakenOrBadName_InOneUndoStep()
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "named.dxf");
            CadTiledViewports tiles = document.TiledViewports;
            Assert.Null(tiles.SplitAs("Two: Vertical", true));
            tiles.Save("Plan");
            tiles.Save("Sections");

            var dialog = new ViewportsViewModel(document) { IsNamedTab = true };
            dialog.Named = "Plan";
            Assert.Equal("Plan", dialog.RenameText);
            dialog.RenameText = "sections";
            Assert.False(dialog.RenameNamed());
            Assert.Equal("Viewport configuration \"Sections\" already exists.", dialog.Message);
            dialog.RenameText = "Plan*";
            Assert.False(dialog.RenameNamed());
            Assert.Equal("Invalid viewport configuration name.", dialog.Message);
            dialog.RenameText = "Plan and details";
            Assert.True(dialog.RenameNamed());

            Assert.Equal(new[] { "Plan and details", "Sections" }, dialog.Names);
            Assert.Equal("Plan and details", dialog.Named);
            Assert.Equal(2, tiles.Saved("Plan and details").Count);
            Assert.True(document.Undo());
            Assert.Equal(new[] { "Plan", "Sections" }, tiles.Names);
        }

        #endregion

        #region Multiline style colours and linetypes

        [Fact]
        public void MlineStyle_SelectColorAndLinetype_ForAnElementAndTheFill_TakeTheDialogsChoice()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string offered = null;
                bool edited = false;
                CadLinetypes.Load(
                    model.Document,
                    CadLinetypeFile.BuiltIn.Where(l => l.Name == "CENTER"),
                    CadLinetypeFile.BuiltInName
                );
                model.ShowMlineStyle = dialog =>
                {
                    dialog.AskNewStyle = naming =>
                    {
                        naming.Name = "Wall";
                        return naming.TryContinue();
                    };
                    dialog.EditStyle = editing =>
                    {
                        Assert.Equal(MlineStyleEditViewModel.SelectColorOption, editing.Colors.Last());
                        Assert.Equal(MlineStyleEditViewModel.SelectColorOption, editing.FillColors.Last());
                        var colors = new List<SelectColorViewModel>();
                        editing.PickColor = d =>
                        {
                            colors.Add(d);
                            d.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30);
                            return true;
                        };
                        editing.Element = editing.Elements[0];
                        editing.ElementColor = MlineStyleEditViewModel.SelectColorOption;
                        Assert.Equal("30", editing.ElementColor);
                        Assert.Contains("30", editing.Colors);
                        Assert.True(Assert.Single(colors).OffersByLayerAndByBlock);

                        editing.PickColor = d =>
                        {
                            d.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 40);
                            return false;
                        };
                        editing.FillColor = MlineStyleEditViewModel.SelectColorOption;
                        Assert.Equal("None", editing.FillColor);

                        editing.PickColor = d =>
                        {
                            d.Color = DxfColor.FromRgb(255, 128, 0);
                            return true;
                        };
                        editing.FillColor = MlineStyleEditViewModel.SelectColorOption;
                        Assert.Equal("30", editing.FillColor);

                        editing.PickLinetype = d =>
                        {
                            offered = d.Linetype;
                            d.SelectedRow = d.Rows.Single(r => r.Name == "CENTER");
                            return true;
                        };
                        editing.ChooseLinetype();
                        Assert.Equal("CENTER", editing.ElementLinetype);
                        edited = true;
                        return editing.TryAccept();
                    };
                    Assert.True(dialog.New());
                    dialog.SetCurrent();
                    return true;
                };

                model.Execute("MLSTYLE");

                Assert.True(edited);
                Assert.Equal("BYLAYER", offered, StringComparer.OrdinalIgnoreCase);

                DxfMlineStyle wall = (DxfMlineStyle)model.Document.Database.CmlstyleID;
                Assert.Equal("WALL", wall.Name);

                Assert.True(wall.Filled);
                Assert.Equal(30, wall.FillColor.ColorIndex);
                DxfMlineStyleElement top = wall.Elements.OrderByDescending(e => e.Offset).First();
                Assert.Equal(30, top.Color.ColorIndex);
                Assert.Equal("CENTER", top.Linetype);
            }
        }

        #endregion

        #region Hatch origin

        [Fact]
        public void Hatch_ClickToSetNewOrigin_ClosesTheDialog_AsksThePoint_AndShowsTheDialogAgainWithIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var shown = new List<HatchSettingsViewModel>();
                model.ShowHatchSettings = dialog =>
                {
                    shown.Add(dialog);

                    if (shown.Count == 1)
                    {
                        Assert.True(dialog.CanPickOrigin);
                        dialog.Scale = "2";
                        dialog.ClickToSetOrigin();
                        return false;
                    }

                    Assert.Same(shown[0], dialog);
                    Assert.True(dialog.IsSpecifiedOrigin);
                    Assert.False(dialog.DefaultToExtents);
                    Assert.Equal("12.5", dialog.OriginX);
                    Assert.Equal("40", dialog.OriginY);
                    Assert.Equal("2", dialog.Scale);
                    dialog.StoreAsDefault = true;
                    return true;
                };

                model.Execute("H");
                model.Execute("T");
                Assert.Equal("Specify origin point:", model.Prompt);
                model.Execute("12.5,40");

                Assert.Equal(2, shown.Count);
                Assert.Equal(12.5, model.Document.HatchSettings.OriginX);
                Assert.Equal(40.0, model.Document.HatchSettings.OriginY);
                Assert.StartsWith("Pick internal point", model.Prompt);
                model.Escape();
            }
        }

        [Fact]
        public void Hatch_CancelAfterThePick_LeavesTheSettingsAsTheyWere()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                int shown = 0;
                model.ShowHatchSettings = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        dialog.ClickToSetOrigin();
                    }

                    return false;
                };

                model.Execute("H");
                model.Execute("T");
                model.Execute("12.5,40");

                Assert.Equal(2, shown);
                Assert.Equal(0.0, model.Document.HatchSettings.OriginX);
                Assert.Equal(CadHatchOriginMode.Current, model.Document.HatchSettings.OriginMode);
                model.Escape();
            }
        }

        #endregion

        #region Insert Object

        [Fact]
        public void InsertObject_TheDialogTakesAFileFromBrowse_AndOkInsertsIt_LinkGreyed_CreateNewListingNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string logo = Path.Combine(folder.FullPath, "logo.png");
                File.WriteAllBytes(logo, Png());
                bool shown = false;
                model.ShowInsertObject = dialog =>
                {
                    shown = true;
                    Assert.True(dialog.IsFromFile);
                    Assert.False(dialog.CanLink);
                    Assert.Empty(dialog.ObjectTypes);
                    dialog.IsCreateNew = true;
                    Assert.False(dialog.CanAccept);
                    dialog.IsFromFile = true;
                    Assert.False(dialog.CanAccept);
                    dialog.BrowseFile = job =>
                    {
                        Assert.Equal("Insert Object", job.Title);
                        job.FileName = logo;
                    };
                    dialog.Browse();
                    Assert.Equal(logo, dialog.File);
                    Assert.True(dialog.CanAccept);
                    return true;
                };

                model.Execute("IO");

                Assert.True(shown);
                Assert.Single(model.Document.Database.ModelSpace.OfType<DxfOle2Frame>());
            }
        }

        [Fact]
        public void InsertObject_CancelInTheDialog_InsertsNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowInsertObject = dialog =>
                {
                    dialog.File = "elsewhere.png";
                    return false;
                };

                model.Execute("INSERTOBJ");

                Assert.Empty(model.Document.Database.ModelSpace.OfType<DxfOle2Frame>());
                Assert.False(model.Document.IsModified);
            }
        }

        private static byte[] Png()
        {
            using (
                var bitmap = new SkiaSharp.SKBitmap(
                    new SkiaSharp.SKImageInfo(8, 6, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul)
                )
            )
            {
                bitmap.Erase(SkiaSharp.SKColors.Red);

                using (SkiaSharp.SKData data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        #endregion

        #region Drawing Recovery Manager

        [Fact]
        public void DrawingRecovery_ThePaletteListsTheCopiesWithTheirTimes_AndOpenOpensTheOnePicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string drawing = model.Document.FileName;
                string copy = Path.Combine(folder.FullPath, "dialogs6.autosave1.dxf");
                File.Copy(drawing, copy);
                File.SetLastWriteTime(copy, new DateTime(2026, 11, 13, 8, 12, 0));
                DrawingRecoveryViewModel palette = null;
                model.ShowDrawingRecovery = p => palette = p;

                model.Execute("DRM");

                Assert.NotNull(palette);
                Assert.False(model.IsCommandRunning);
                BackupFileRow row = Assert.Single(palette.Copies);
                Assert.Equal("dialogs6.autosave1.dxf", row.Name);
                Assert.Equal("dialogs6.dxf", row.Original);
                Assert.Equal("2026-11-13 08:12", row.Saved);
                Assert.Equal(folder.FullPath.TrimEnd('\\', '/'), row.Folder.TrimEnd('\\', '/'));
                Assert.Same(row, palette.Selected);
                Assert.True(palette.Open());
                Assert.Equal(Path.GetFullPath(copy), Path.GetFullPath(model.Document.FileName));
            }
        }

        [Fact]
        public void DrawingRecovery_WithNoCopy_ThePaletteSaysSo()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                DrawingRecoveryViewModel palette = null;
                model.ShowDrawingRecovery = p => palette = p;

                model.Execute("DRAWINGRECOVERY");

                Assert.Empty(palette.Copies);
                Assert.Equal("No backup files were found beside the drawings listed.", palette.Message);
                Assert.False(palette.Open());
            }
        }

        #endregion

        #region Rename with wildcards

        [Fact]
        public void Rename_AWildcardOldName_RenamesEveryItemItMatches_CarryingWhatTheWildcardsMatched()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowRename = dialog =>
                {
                    dialog.Kind = "Layers";
                    dialog.Item = "S-BEAM";
                    dialog.OldName = "S-*";
                    dialog.NewName = "A-*";
                    Assert.True(dialog.RenameTo());
                    Assert.Equal(new[] { "A-BEAM", "A-COLUMN", "WALLS" }, dialog.Items);
                    Assert.Equal(2, dialog.Renames.Count);

                    dialog.OldName = "*A*";
                    dialog.NewName = "*B*";
                    Assert.True(dialog.RenameTo());
                    Assert.Equal(new[] { "B-BEAM", "B-COLUMN", "WBLLS" }, dialog.Items);
                    return true;
                };

                model.Execute("REN");

                Assert.Equal("B-BEAM", model.Document.ModelSpace.Block.OfType<DxfLine>().Single().Layer);
                Assert.True(model.Document.Database.LayerTable.Has("B-COLUMN"));
                Assert.True(model.Document.Database.LayerTable.Has("WBLLS"));
                Assert.False(model.Document.Database.LayerTable.Has("S-BEAM"));
            }
        }

        [Fact]
        public void Rename_AWildcardOldName_RefusesAllWhereOneWouldBeTaken_OrNothingMatches()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowRename = dialog =>
                {
                    dialog.Kind = "Layers";
                    dialog.OldName = "S-*";
                    dialog.NewName = "WALLS";
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("\"WALLS\" already exists.", dialog.Message);
                    dialog.OldName = "X-*";
                    dialog.NewName = "Y-*";
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("No item matches X-*.", dialog.Message);
                    dialog.OldName = "S-*";
                    dialog.NewName = "S|*";
                    Assert.False(dialog.RenameTo());
                    Assert.Equal("Invalid name.", dialog.Message);
                    Assert.Equal(new[] { "S-BEAM", "S-COLUMN", "WALLS" }, dialog.Items);
                    Assert.Empty(dialog.Renames);
                    return false;
                };

                model.Execute("REN");
            }
        }

        #endregion

        #region Check Spelling

        [Fact]
        public void Spell_UndoTakesBackTheLastAnswer_TheWordAskedAgainAndItsChangeDropped()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowCheckSpelling = dialog =>
                {
                    Assert.False(dialog.CanUndo);
                    dialog.Start();
                    Assert.Equal("Helo", dialog.Word);
                    dialog.Change();
                    Assert.Equal("wrold", dialog.Word);
                    Assert.True(dialog.CanUndo);
                    dialog.Undo();
                    Assert.Equal("Helo", dialog.Word);
                    Assert.Equal(0, dialog.Check.ChangedCount);
                    dialog.Ignore();
                    dialog.Replacement = "world";
                    dialog.Change();
                    Assert.False(dialog.IsChecking);
                    dialog.Undo();
                    Assert.Equal("wrold", dialog.Word);
                    dialog.Replacement = "world";
                    dialog.Change();
                };

                model.Execute("SP");

                Assert.Equal("Helo world", model.Document.ModelSpace.Block.OfType<DxfDBText>().Single().TextString);
            }
        }

        [Fact]
        public void Spell_Settings_IgnoreCapitalizedWords_PassesHeloOver_AndBlockAttributesOffLeavesThemOut()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowCheckSpelling = dialog =>
                {
                    dialog.AskSettings = settings =>
                    {
                        Assert.True(settings.IgnoreUppercase);
                        Assert.True(settings.IgnoreWithNumbers);
                        Assert.True(settings.BlockAttributes);
                        Assert.False(settings.CanCheckDimensionText);
                        Assert.False(settings.CanCheckExternalReferences);
                        settings.IgnoreCapitalized = true;
                        return true;
                    };
                    dialog.OpenSettings();
                    Assert.True(dialog.Check.Settings.IgnoreCapitalized);
                    dialog.Start();
                    Assert.Equal("wrold", dialog.Word);
                };

                model.Execute("SP");
            }
        }

        #endregion

        [Fact]
        public void TheNewAndChangedWindows_LoadTheirLayoutsAndLayOutWithNoWindowShown()
        {
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    CadDocument document = CadDocument.From(Drawing(), "windows.dxf");
                    string[] types = { "Picture (*.bmp;*.png)" };
                    var job = new CadExportJob("Insert Object", types, types[0], null);
                    Lay(new InsertObjectWindow(new InsertObjectViewModel(job)).Body);
                    Lay(new InsertObjectWindow(new InsertObjectViewModel(job) { IsCreateNew = true }).Body);
                    Lay(new DrawingRecoveryWindow(new DrawingRecoveryViewModel(new BackupFileRow[0], _ => { })).Body);
                    Lay(new CheckSpellingSettingsWindow(new CheckSpellingSettingsViewModel(null)).Body);
                    Lay(new ViewportsWindow(new ViewportsViewModel(document)).Body);
                    Lay(new ViewportsWindow(new ViewportsViewModel(document) { IsNamedTab = true }).Body);
                    Lay(
                        new HatchSettingsWindow(
                            new HatchSettingsViewModel(document, new CadHatchSettings(), false)
                        ).Body
                    );
                    var style = new MlineStyleEditViewModel(
                        "New Multiline Style: A",
                        new DxfMlineStyleSettings(),
                        false,
                        null
                    );
                    Lay(new MlineStyleEditWindow(style).Body);
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static void Lay(System.Windows.FrameworkElement element)
        {
            element.Measure(new System.Windows.Size(2000, 2000));
            element.Arrange(new System.Windows.Rect(element.DesiredSize));
            Assert.True(element.DesiredSize.Width > 0);
        }
    }
}
