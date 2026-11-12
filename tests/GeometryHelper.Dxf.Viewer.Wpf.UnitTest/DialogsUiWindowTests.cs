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
    /// The dialogs of the "dialogs-ui" round driven with no window: Edit Drawing Scales with Add Scale and Edit Scale
    /// (SCALELISTEDIT), a layer renamed in place in the Layer manager, New Layer State to Save with its description and Edit
    /// Layer State, and Input Search Options (INPUTSEARCHOPTIONS, -INPUTSEARCHOPTIONS, INPUTSEARCHOPTIONFLAGS) with what it
    /// changes in AutoComplete; and each window made over its view model.
    /// </summary>
    public sealed class DialogsUiWindowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>A metric drawing of R2013 with the layers 0, WALLS red, DIM green, TEXT and Defpoints, a line on TEXT.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS", ColorIndex = 1 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DIM", ColorIndex = 3 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "TEXT", ColorIndex = 4 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Defpoints", ColorIndex = 7 });
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "TEXT" }
            );
            return db;
        }

        private MainViewModel Opened()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("plan-" + Guid.NewGuid().ToString("N") + ".dxf", Drawing())));
            return model;
        }

        private static string[] ScaleNames(MainViewModel model) =>
            model
                .Document.Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .Select(s => s.Name)
                .ToArray();

        // ---- Edit Drawing Scales

        [Fact]
        public void EditDrawingScales_AddEditMoveDeleteReset_AndOkWritesTheListAsOneUndoStep()
        {
            MainViewModel model = Opened();
            model.Execute("-SCALELISTEDIT");
            model.Execute("R");
            model.Execute("Y");
            model.Execute("E");
            string[] before = ScaleNames(model);
            Assert.Equal(17, before.Length);

            model.ShowScaleList = dialog =>
            {
                Assert.Equal("17 scales", dialog.Summary);
                Assert.Equal("1:1", dialog.Selected.Name);
                Assert.Equal("Selected scale: 1:1", dialog.SelectedTitle);
                Assert.Equal("1 paper unit = 1 drawing unit", dialog.Sentence);

                // 1:1 is the unit scale and the current one: Delete greyed, Move Up greyed on the first row.
                Assert.False(dialog.CanDelete);
                Assert.False(dialog.CanMoveUp);
                Assert.False(dialog.Delete());

                // Add Scale: a name used refused, then 1:25 added at the end and chosen.
                dialog.AskScale = scale =>
                {
                    Assert.Equal("Add Scale", scale.Title);
                    Assert.Equal("1", scale.PaperUnits);
                    scale.Name = "1:50";
                    scale.DrawingUnits = "25";
                    Assert.False(scale.Ok());
                    Assert.Equal("A scale named 1:50 already exists. Choose another name.", scale.Message);
                    scale.Name = "1:25 site plan";
                    scale.DrawingUnits = "0";
                    Assert.False(scale.Ok());
                    scale.DrawingUnits = "25";
                    return scale.Ok();
                };
                Assert.True(dialog.Add());
                Assert.Equal("1:25 site plan", dialog.Scales.Last().Name);
                Assert.Same(dialog.Scales.Last(), dialog.Selected);
                Assert.Equal("1 paper unit = 25 drawing units", dialog.Sentence);
                Assert.False(dialog.CanMoveDown);
                Assert.Equal("18 scales", dialog.Summary);

                // Edit Scale over 1:50: its name kept, units changed; Move Up twice.
                dialog.Selected = dialog.Scales.Single(s => s.Name == "1:50");
                dialog.AskScale = scale =>
                {
                    Assert.Equal("Edit Scale", scale.Title);
                    Assert.Equal("1:50", scale.Name);
                    Assert.True(scale.CanChangeUnits);
                    scale.Name = "1:50 detail";
                    return scale.Ok();
                };
                Assert.True(dialog.Edit());
                int at = dialog.Scales.IndexOf(dialog.Selected);
                dialog.MoveUp();
                dialog.MoveUp();
                Assert.Equal(at - 2, dialog.Scales.IndexOf(dialog.Selected));

                // Delete 1:100; Reset's question answered No leaves the list.
                dialog.Selected = dialog.Scales.Single(s => s.Name == "1:100");
                Assert.True(dialog.Delete());
                dialog.ConfirmReset = () => false;
                Assert.False(dialog.Reset());
                Assert.Equal(17, dialog.Scales.Count);
                dialog.Ok();
                return true;
            };

            model.Execute("SCALELISTEDIT");

            string[] names = ScaleNames(model);
            Assert.Contains("1:50 detail", names);
            Assert.Contains("1:25 site plan", names);
            Assert.DoesNotContain("1:100", names);
            Assert.Equal("1:25 site plan", names.Last());
            Assert.Equal("SCALELISTEDIT", model.Document.NextUndo.Name);
            Assert.True(model.Document.Undo());
            Assert.Equal(before, ScaleNames(model));

            // Reset answered Yes: the custom scale goes, the defaults come back; Cancel writes nothing.
            model.ShowScaleList = dialog =>
            {
                dialog.AskScale = scale =>
                {
                    scale.Name = "GH";
                    return scale.Ok();
                };
                dialog.Add();
                dialog.ConfirmReset = () => true;
                Assert.True(dialog.Reset());
                Assert.DoesNotContain(dialog.Scales, s => s.Name == "GH");
                Assert.Equal(17, dialog.Scales.Count);
                dialog.Cancel();
                return true;
            };
            object undo = model.Document.NextUndo;
            model.Execute("SCALELISTEDIT");
            Assert.Same(undo, model.Document.NextUndo);
        }

        // ---- Rename in the Layer manager

        [Fact]
        public void LayerManager_RenamesInPlace_AndShowsAutoCADsRefusals()
        {
            MainViewModel model = Opened();
            var shown = new List<string>();
            var manager = new LayerManagerViewModel(model.Document) { ShowMessage = shown.Add };

            // Layer 0: the cell stays shut, the message at once; Rename Layer greyed.
            LayerRowViewModel zero = manager.Rows.Single(r => r.Name == "0");
            manager.SelectedRow = zero;
            Assert.False(manager.RenameCommand.CanExecute(null));
            Assert.False(manager.BeginRename());
            Assert.False(zero.IsRenaming);
            Assert.Equal("Cannot rename layer \"0\".", shown.Last());

            // Defpoints can be renamed, as AutoCAD's -RENAME renames it.
            manager.SelectedRow = manager.Rows.Single(r => r.Name == "Defpoints");
            Assert.True(manager.RenameCommand.CanExecute(null));

            // TEXT: the cell opens on its name; a name used, then a name with <, refused with the cell left open.
            LayerRowViewModel text = manager.Rows.Single(r => r.Name == "TEXT");
            manager.SelectedRow = text;
            manager.RenameCommand.Execute(null);
            Assert.True(text.IsRenaming);
            Assert.Equal("TEXT", text.EditName);
            text.EditName = "dim";
            Assert.False(manager.CommitRename(text));
            Assert.Equal("A layer named \"DIM\" already exists.", shown.Last());
            Assert.True(text.IsRenaming);
            text.EditName = "A<B";
            Assert.False(manager.CommitRename(text));
            Assert.Equal("Invalid layer name.", shown.Last());

            // Esc puts the old name back.
            manager.CancelRename(text);
            Assert.False(text.IsRenaming);
            Assert.Equal("TEXT", text.EditName);

            // Enter keeps the name typed, the spaces around it taken off: the line moves with it, one undo step.
            Assert.True(manager.BeginRename(text));
            text.EditName = "  TEXT-NOTES ";
            Assert.True(manager.CommitRename(text));
            Assert.Equal("TEXT-NOTES", manager.SelectedRow.Name);
            Assert.Contains(manager.Rows, r => r.Name == "TEXT-NOTES");
            Assert.Equal("TEXT-NOTES", model.Document.Database.ModelSpace.OfType<DxfLine>().Single().Layer);
            Assert.Equal("LAYER", model.Document.NextUndo.Name);
            manager.Dispose();
        }

        // ---- New Layer State to Save and Edit Layer State

        [Fact]
        public void LayerStates_NewWithADescription_ReplaceAsked_AndEditLayerStateWritesTheStateOnly()
        {
            MainViewModel model = Opened();
            model.ShowLayerStates = dialog =>
            {
                dialog.AskNewState = state =>
                {
                    Assert.Equal("Layer state 1", state.Name);
                    Assert.Empty(state.Names);
                    state.Name = "Plan - no dims";
                    state.Description = "DIM and TEXT off for the client set";
                    return state.CanOk;
                };
                Assert.True(dialog.New());
                Assert.Equal("DIM and TEXT off for the client set", dialog.Selected.Description);

                // The same name again: replaced only on Yes, its description too.
                string asked = null;
                dialog.AskNewState = state =>
                {
                    Assert.Equal(new[] { "Plan - no dims" }, state.Names);
                    state.Name = "plan - NO dims";
                    state.Description = "Replaced";
                    return true;
                };
                dialog.ConfirmReplace = name =>
                {
                    asked = name;
                    return false;
                };
                Assert.False(dialog.New());
                Assert.Equal("plan - NO dims", asked);
                Assert.Equal("DIM and TEXT off for the client set", dialog.Selected.Description);
                dialog.ConfirmReplace = name => true;
                Assert.True(dialog.New());
                Assert.Single(dialog.States);
                Assert.Equal("Replaced", dialog.Selected.Description);

                // Edit Layer State: DIM and TEXT off in the state, WALLS removed then put back, Linetype not restored.
                dialog.ShowEditState = edit =>
                {
                    Assert.Equal("Edit Layer State - Plan - no dims", edit.Title);
                    Assert.Equal(new[] { "0", "Defpoints", "DIM", "TEXT", "WALLS" }, edit.Rows.Select(r => r.Name));
                    Assert.Equal("5 of 5 layers of the drawing", edit.Summary);
                    Assert.All(edit.Rows, r => Assert.False(r.Differs));
                    EditLayerStateRowViewModel dim = edit.Rows.Single(r => r.Name == "DIM");
                    dim.IsOn = false;
                    Assert.True(dim.Differs);
                    edit.Rows.Single(r => r.Name == "TEXT").IsOn = false;
                    dim.Weight = "0.18";
                    dim.Transparency = "95";
                    Assert.Equal("0", dim.Transparency);
                    dim.Transparency = "40";

                    edit.Rows.Single(r => r.Name == "WALLS").IsChosen = true;
                    Assert.Equal(1, edit.RemoveLayers());
                    edit.PickLayers = offered =>
                    {
                        Assert.Equal(new[] { "WALLS" }, offered);
                        return offered;
                    };
                    Assert.Equal(1, edit.AddLayers());
                    Assert.Contains(edit.Rows, r => r.Name == "WALLS" && r.IsChosen);

                    edit.Properties.Single(p => p.Name == "Linetype").IsChecked = false;
                    Assert.False(edit.RestoresLinetype);
                    Assert.True(edit.RestoresColor);
                    edit.SetAll(false);
                    Assert.False(dim.Differs);
                    edit.SetAll(true);
                    edit.Properties.Single(p => p.Name == "Linetype").IsChecked = false;
                    return true;
                };
                Assert.True(dialog.Edit());
                Assert.Equal("No", dialog.Selected.Same);
                Assert.Equal(0, dialog.Selected.Row.Mask & 0x40);
                Assert.False(model.Document.Database.LayerTable["DIM"].IsOff);
                dialog.Restore();
                return true;
            };

            // The drawing's layers stay as they were until the state is restored: Restore turns DIM and TEXT off.
            model.Execute("LAYERSTATE");

            DxfDatabase db = model.Document.Database;
            Assert.True(db.LayerTable["DIM"].IsOff);
            Assert.True(db.LayerTable["TEXT"].IsOff);
            Assert.False(db.LayerTable["WALLS"].IsOff);
            Assert.Equal(DxfLineWeight.LineWeight018, db.LayerTable["DIM"].LineWeight);
        }

        // ---- Input Search Options

        [Fact]
        public void InputSearchOptions_ShowsTheSettings_RefusesNumbersOutOfRange_AndOkKeepsThem()
        {
            MainViewModel model = Opened();
            int changed = 0;
            model.SettingsChanged += (s, e) => changed++;
            model.ShowInputSearchOptions = dialog =>
            {
                Assert.True(dialog.AutoComplete);
                Assert.True(dialog.ByFrequency);
                Assert.Equal("3", dialog.RememberAfter);
                Assert.Equal("300", dialog.Delay);
                Assert.Equal(
                    new[] { "Block", "Layer", "Hatch", "Visual Style", "Text Style", "Dim Style" },
                    dialog.ContentTypes.Select(t => t.Name)
                );

                // AutoCAD 2021's dialog takes 1 to 50 mistypes and 100 to 10 000 milliseconds, refusing others in its words.
                dialog.RememberAfter = "51";
                Assert.False(dialog.Ok());
                Assert.Equal("Invalid input. Mistypes value must be between 1 and 50", dialog.Message);
                dialog.RememberAfter = "0";
                Assert.False(dialog.Ok());
                dialog.RememberAfter = "50";
                Assert.True(dialog.Ok());
                dialog.RememberAfter = "1";
                dialog.Delay = "50";
                Assert.False(dialog.Ok());
                Assert.Equal("Invalid input. Delay time must be between 100 and 10000 milliseconds", dialog.Message);
                dialog.Delay = "500";

                dialog.Alphabetically = true;
                Assert.False(dialog.ByFrequency);
                dialog.Separate = true;
                dialog.SelectedContentType = dialog.ContentTypes.Single(t => t.Name == "Layer");
                Assert.True(dialog.CanMoveUp);
                dialog.MoveUp();
                Assert.Equal("Layer", dialog.ContentTypes[0].Name);
                Assert.False(dialog.CanMoveUp);
                dialog.ContentTypes.Single(t => t.Name == "Hatch").IsSearched = false;
                dialog.SearchContent = false;
                Assert.False(dialog.CanMoveDown);
                dialog.SearchContent = true;
                return dialog.Ok();
            };

            model.Execute("INPUTSEARCHOPTIONS");

            GlobalSettings settings = model.Settings;
            Assert.True(settings.SortAlphabetically);
            Assert.True(settings.SeparateSystemVariables);
            Assert.Equal(1, settings.AutoCorrectMistypes);
            Assert.Equal(500, settings.AutoCompleteDelay);
            Assert.Equal("Layer", settings.ContentTypes[0].Name);
            Assert.False(settings.ContentTypes.Single(t => t.Name == "Hatch").IsSearched);
            Assert.True(changed > 0);

            // Cancel keeps the settings as they are.
            model.ShowInputSearchOptions = dialog =>
            {
                dialog.AutoComplete = false;
                return false;
            };
            model.Execute("INPUTSEARCHOPTIONS");
            Assert.True(model.Settings.AutoComplete);

            // The settings file keeps them.
            string path = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);
            Assert.True(read.SortAlphabetically);
            Assert.True(read.SeparateSystemVariables);
            Assert.Equal(1, read.AutoCorrectMistypes);
            Assert.Equal(settings.ContentTypes.Select(t => t.ToString()), read.ContentTypes.Select(t => t.ToString()));
        }

        [Fact]
        public void TypedInputSearchOptions_AsksAsAutoCADsConsole_AndTheFlagsVariableFollows()
        {
            MainViewModel model = Opened();

            // AutoCAD 2021's console, word for word.
            model.Execute("-INPUTSEARCHOPTIONS");
            Assert.Contains(
                "Current settings: AutoComplete = Y, AutoCorrect = Y, System variable = Y, Content = Y, Midstring = Y, Delay=0.30",
                model.History
            );
            Assert.Equal(
                "Enter an input search Option [autoComplete/autocoRrect/System variables/conTent/Midstring/Delay]:",
                model.Prompt
            );
            model.Execute("R");
            Assert.Equal("Automatically correct a mistake as you type? [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");
            Assert.False(model.Settings.AutoCorrect);
            model.Execute("S");
            Assert.Equal("Search system variables as you type? [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");
            model.Execute("D");
            Assert.Equal("Specify the delay time in seconds <0.30>:", model.Prompt);
            model.Execute("20");
            Assert.Contains("Invalid. Maximum delay is 10 seconds", model.History);
            Assert.Equal("Specify the delay time in seconds <0.30>:", model.Prompt);
            model.Execute("0.05");
            Assert.Equal(300, model.Settings.AutoCompleteDelay);
            model.Execute("D");
            model.Execute("2");
            Assert.Equal(2000, model.Settings.AutoCompleteDelay);
            Assert.Contains(
                "Current settings: AutoComplete = Y, AutoCorrect = N, System variable = N, Content = Y, Midstring = Y, Delay=2.00",
                model.History
            );
            model.Execute("");
            Assert.False(model.IsCommandRunning);

            // INPUTSEARCHOPTIONFLAGS: 1 AutoComplete, 2 AutoCorrect, 4 variables, 8 content, 16 mid-string, as AutoCAD reads it.
            model.Execute("INPUTSEARCHOPTIONFLAGS");
            Assert.Equal("Enter new value for INPUTSEARCHOPTIONFLAGS <25>:", model.Prompt);
            model.Execute("64");
            Assert.Contains("Requires an integer between 0 and 31.", model.History);
            model.Execute("15");
            Assert.False(model.Settings.MidStringSearch);
            Assert.True(model.Settings.AutoCorrect);
            Assert.True(model.Settings.SearchSystemVariables);
        }

        [Fact]
        public void AutoComplete_FollowsAlphabeticallySeparateVariablesAndTheContentTypes()
        {
            MainViewModel model = Opened();
            GlobalSettings settings = model.Settings.Clone();

            for (int i = 0; i < 3; i++)
            {
                settings.CountCommandUse("LAYOUT");
            }

            // Search Content lists at the command line alone, with DYN off.
            settings.DynamicInput = false;
            settings.SortAlphabetically = true;
            model.ApplySettings(settings);
            model.CommandText = "LA";
            model.ShowSuggestions();

            // Alphabetically: LAYOUT's uses count for nothing; after the alias's LAYER, the commands by name.
            List<string> prefix = model
                .Suggestions.Skip(1)
                .TakeWhile(r => !r.StartsGroup && !r.IsVariable)
                .Select(r => r.Name)
                .ToList();
            Assert.Equal(prefix.OrderBy(n => n, StringComparer.Ordinal), prefix);

            // Separate commands and system variables: as AutoCAD 2021, the variables wait under a header of their own at the
            // end, collapsed, below a rule, until Tab shows them, the first of them chosen.
            settings = model.Settings.Clone();
            settings.SeparateSystemVariables = true;
            settings.SearchContent = false;
            model.ApplySettings(settings);
            model.ShowSuggestions();
            IReadOnlyList<CommandSuggestionViewModel> rows = model.Suggestions;
            Assert.DoesNotContain(rows, r => r.IsVariable);
            CommandSuggestionViewModel header = rows[rows.Count - 1];
            Assert.True(header.IsHeader);
            Assert.Equal("System Variables", header.Name);
            Assert.False(header.IsExpanded);
            Assert.True(header.StartsGroup);
            Assert.True(int.Parse(header.Note, System.Globalization.CultureInfo.InvariantCulture) > 0);

            Assert.True(model.TabSuggestions());
            rows = model.Suggestions;
            int first = rows.ToList().FindIndex(r => r.IsVariable);
            Assert.Equal(rows.ToList().IndexOf(header) + 1, first);
            Assert.True(header.IsExpanded);
            Assert.All(rows.Skip(first), r => Assert.True(r.IsVariable, r.Name));
            Assert.Same(rows[first], model.SelectedSuggestion);
            Assert.False(model.TabSuggestions());

            // A new list starts collapsed again; with Separate off, Tab leaves the list as it is.
            model.ShowSuggestions();
            Assert.DoesNotContain(model.Suggestions, r => r.IsVariable);
            settings = model.Settings.Clone();
            settings.SeparateSystemVariables = false;
            model.ApplySettings(settings);
            model.ShowSuggestions();
            Assert.Contains(model.Suggestions, r => r.IsVariable);
            Assert.DoesNotContain(model.Suggestions, r => r.IsHeader);
            Assert.False(model.TabSuggestions());

            // The content types in their order, Hatch not searched: the layers before the blocks.
            settings = model.Settings.Clone();
            settings.SearchContent = true;
            settings.ContentTypes = new[]
            {
                new GlobalContentType("Layer", true),
                new GlobalContentType("Block", true),
                new GlobalContentType("Hatch", false),
                new GlobalContentType("Visual Style", true),
                new GlobalContentType("Text Style", true),
                new GlobalContentType("Dim Style", true),
            };
            model.ApplySettings(settings);
            model.CommandText = "LAYER";
            model.ShowSuggestions();
            List<string> kinds = model
                .Suggestions.Where(r => r.ContentType != null)
                .Select(r => r.ContentType)
                .Distinct()
                .ToList();
            Assert.Equal("Layer", kinds[0]);
            model.CommandText = "HATCH";
            model.ShowSuggestions();
            Assert.DoesNotContain(model.Suggestions, r => r.ContentType == "Hatch");
        }

        [Fact]
        public void AutoCorrect_OffersTheCommandAfterTheMistypesSet()
        {
            var settings = new GlobalSettings { AutoCorrectMistypes = 2 };

            Assert.False(settings.NoteCorrection("LIEN", "LINE"));
            Assert.True(settings.NoteCorrection("LIEN", "LINE"));
            Assert.Equal("LINE", settings.CorrectionOf("lien"));
            settings.AutoCorrectMistypes = 3;
            Assert.Null(settings.CorrectionOf("lien"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.AutoCorrectMistypes = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.AutoCorrectMistypes = 51);
            settings.AutoCorrectMistypes = 1;
            Assert.True(settings.NoteCorrection("CRICLE", "CIRCLE"));
            Assert.Throws<ArgumentException>(() =>
                settings.ContentTypes = new[] { new GlobalContentType("Layer", true) }
            );
        }

        // ---- The windows

        [Fact]
        public void EveryNewWindow_IsMadeOverItsViewModel_AsTheScreenshotModeDrawsIt()
        {
            string path = _folder.Write("plan-windows.dxf", Drawing());
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(path));
                    object shown = null;
                    model.ShowScaleList = d =>
                    {
                        shown = d;
                        return true;
                    };
                    model.Execute("SCALELISTEDIT");
                    var windows = new List<Window> { new ScaleListEditWindow((ScaleListEditViewModel)shown) };
                    windows.Add(new ScaleEditWindow(new ScaleEditViewModel(new[] { "1:1" })));
                    windows.Add(new InputSearchOptionsWindow(new InputSearchOptionsViewModel(model.Settings)));
                    windows.Add(new NewLayerStateWindow(new NewLayerStateViewModel(new[] { "Plan" }, "Layer state 2")));
                    model.ShowLayerStates = d =>
                    {
                        Assert.True(d.Request.New("Plan", string.Empty));
                        d.Request.Selected = "Plan";
                        shown = new EditLayerStateViewModel(d.Request.BeginEdit(), model.Document);
                        return true;
                    };
                    model.Execute("LAYERSTATE");
                    windows.Add(new EditLayerStateWindow((EditLayerStateViewModel)shown));
                    windows.Add(new LayerManagerWindow(new LayerManagerViewModel(model.Document)));

                    foreach (Window window in windows)
                    {
                        var body = (FrameworkElement)window.Content;
                        window.Content = null;
                        body.DataContext = window.DataContext;
                        double width = double.IsNaN(window.Width) ? 400 : window.Width;
                        double height = double.IsNaN(window.Height) ? 400 : window.Height;
                        body.Measure(new Size(width, height));
                        body.Arrange(new Rect(0, 0, width, height));
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
