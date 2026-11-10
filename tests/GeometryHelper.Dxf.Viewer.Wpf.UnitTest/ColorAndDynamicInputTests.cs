using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// AutoCAD's Select Color dialog driven with no window, from the Layer manager and from Properties; and dynamic input
    /// (DYN, F12) with the command line shown or hidden (Ctrl+9).
    /// </summary>
    public class ColorAndDynamicInputTests
    {
        private static MainViewModel Room(TestFolder folder, out string bottom)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out bottom, out _))));
            return model;
        }

        /// <summary>The groups of a layer's record in a DXF file written, as code and value.</summary>
        private static List<KeyValuePair<int, string>> LayerGroups(string path, string layer)
        {
            string[] lines = File.ReadAllLines(path);
            var pairs = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                pairs.Add(new KeyValuePair<int, string>(int.Parse(lines[i].Trim(), System.Globalization.CultureInfo.InvariantCulture), lines[i + 1].Trim()));
            }

            int at = pairs.FindIndex(p => p.Key == 2 && p.Value == layer);
            Assert.True(at > 0, "The file has no layer " + layer + ".");
            int start = pairs.FindLastIndex(at, p => p.Key == 0);
            int end = pairs.FindIndex(at, p => p.Key == 0);
            return pairs.GetRange(start, end - start);
        }

        [Fact]
        public void ThePaletteHoldsEveryIndexOnceInAutoCadsOrder_EachTheColourTheCanvasDraws()
        {
            var dialog = new SelectColorViewModel(null, false);

            Assert.Equal(Enumerable.Range(1, 9), dialog.StandardColors.Select(s => s.Index));
            Assert.Equal(Enumerable.Range(250, 6), dialog.GreyColors.Select(s => s.Index));
            Assert.Equal(240, dialog.GridColors.Count);
            Assert.Equal(Enumerable.Range(10, 240), dialog.GridColors.Select(s => s.Index).OrderBy(i => i));

            // A column per hue, 10 to 240; the full colours at the top, the palest at the bottom.
            Assert.Equal(Enumerable.Range(1, 24).Select(h => h * 10), dialog.GridColors.Take(24).Select(s => s.Index));
            Assert.Equal(30, dialog.GridColors[2].Index);
            Assert.Equal(18, dialog.GridColors[4 * 24].Index);
            Assert.Equal(19, dialog.GridColors[5 * 24].Index);
            Assert.Equal(11, dialog.GridColors[9 * 24].Index);
            Assert.Equal(0xFF7F00, dialog.GridColors[2].Rgb);

            // Each swatch is the colour the canvas draws a layer of that index in.
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                foreach (AciSwatchViewModel swatch in dialog.StandardColors.Concat(dialog.GridColors).Concat(dialog.GreyColors))
                {
                    Assert.True(CadLayerEditor.SetColor(model.Document, "COLUMNS", swatch.Index).Succeeded);
                    Assert.Equal(swatch.Rgb, model.Document.Layers.Single(l => l.Name == "COLUMNS").Rgb);
                }
            }
        }

        [Fact]
        public void Aci30PickedForANewLayerIsWrittenToGroup62_NegativeWhileOff_AndUndoTakesItBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    string name = manager.NewLayer();
                    SelectColorViewModel shown = null;
                    manager.PickColor = dialog =>
                    {
                        shown = dialog;
                        dialog.PickIndex(30);
                        return true;
                    };

                    manager.Rows.Single(r => r.Name == name).ChooseColorCommand.Execute(null);

                    Assert.Equal("Index color: 7", shown.OriginalText);
                    Assert.False(shown.OffersByLayerAndByBlock);
                    Assert.False(shown.ByLayerCommand.CanExecute(null));
                    Assert.Equal("Index color: 30", shown.ColorText);
                    LayerRowViewModel row = manager.Rows.Single(r => r.Name == name);
                    Assert.Equal("Color 30", row.ColorName);
                    Assert.Equal(0xFF7F00, row.Rgb);

                    string path = Path.Combine(folder.FullPath, "written.dxf");
                    model.Document.Database.Save(path);
                    List<KeyValuePair<int, string>> groups = LayerGroups(path, name);
                    Assert.Contains(new KeyValuePair<int, string>(62, "30"), groups);
                    Assert.DoesNotContain(groups, g => g.Key == 420);

                    row.IsOn = false;
                    model.Document.Database.Save(path);
                    Assert.Contains(new KeyValuePair<int, string>(62, "-30"), LayerGroups(path, name));

                    model.Undo();
                    model.Undo();

                    Assert.Equal(7, model.Document.Database.LayerTable[name].ColorIndex);
                    Assert.Equal("White", manager.Rows.Single(r => r.Name == name).ColorName);
                }
            }
        }

        [Fact]
        public void Rgb10_20_30PickedForANewLayerIsWrittenToGroup420_AndUndoTakesItBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    string name = manager.NewLayer();
                    manager.PickColor = dialog =>
                    {
                        dialog.IsTrueColorTab = true;
                        dialog.Red = 10;
                        dialog.Green = 20;
                        dialog.Blue = 30;
                        return true;
                    };

                    manager.Rows.Single(r => r.Name == name).ChooseColorCommand.Execute(null);

                    DxfLayerTableRecord layer = model.Document.Database.LayerTable[name];
                    Assert.Equal(0x0A141E, layer.TrueColor);
                    Assert.Equal("10,20,30", manager.Rows.Single(r => r.Name == name).ColorName);
                    Assert.Equal(0x0A141E, manager.Rows.Single(r => r.Name == name).Rgb);
                    string path = Path.Combine(folder.FullPath, "written.dxf");
                    model.Document.Database.Save(path);
                    List<KeyValuePair<int, string>> groups = LayerGroups(path, name);
                    Assert.Contains(new KeyValuePair<int, string>(420, "660510"), groups);
                    Assert.Contains(new KeyValuePair<int, string>(62, layer.Color.ColorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)), groups);

                    model.Undo();

                    Assert.Null(model.Document.Database.LayerTable[name].TrueColor);
                    Assert.Equal(7, model.Document.Database.LayerTable[name].ColorIndex);

                    // Cancel leaves the layer as it was.
                    manager.PickColor = dialog =>
                    {
                        dialog.PickIndex(1);
                        return false;
                    };
                    manager.Rows.Single(r => r.Name == name).ChooseColorCommand.Execute(null);

                    Assert.Equal(7, model.Document.Database.LayerTable[name].ColorIndex);
                }
            }
        }

        [Fact]
        public void ATrueColourIsRefusedInADrawingOlderThanAutoCad2004_AndByLayerOnALayer()
        {
            var db = new DxfDatabase("AC1015");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "OLD", ColorIndex = 3 });
            CadDocument document = CadDocument.From(db, "old.dxf");

            Assert.False(CadLayerEditor.SetColor(document, "OLD", DxfColor.FromRgb(10, 20, 30)).Succeeded);
            Assert.False(CadLayerEditor.SetColor(document, "OLD", DxfColor.ByLayer).Succeeded);
            Assert.True(CadLayerEditor.SetColor(document, "OLD", DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30)).Succeeded);
            Assert.Equal(30, db.LayerTable["OLD"].ColorIndex);
        }

        [Fact]
        public void TheTrueColorTabKeepsRgbHslAndHexTogether_AndTheColorBoxTakesNamesAndNumbers()
        {
            var dialog = new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5), true);

            Assert.False(dialog.IsTrueColorTab);
            Assert.Equal("Index color: 5", dialog.OriginalText);
            Assert.True(dialog.StandardColors[4].IsSelected);

            // A slider giving back the value it was given leaves the colour of the index as it is.
            int blue = dialog.Blue;
            int hue = dialog.Hue;
            dialog.Blue = blue;
            dialog.Hue = hue;

            Assert.True(dialog.Color.IsByAci);

            dialog.Hex = "#FF8000";

            Assert.Equal(new[] { 255, 128, 0 }, new[] { dialog.Red, dialog.Green, dialog.Blue });
            Assert.Equal(new[] { 30, 100, 50 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
            Assert.True(dialog.Color.IsByColor);
            Assert.Equal("Red, Green, Blue: 255,128,0", dialog.ColorText);
            Assert.False(dialog.StandardColors[4].IsSelected);

            dialog.Hue = 120;

            Assert.Equal("#00FF00", dialog.Hex);
            Assert.Equal(120, dialog.Hue);

            dialog.Hex = "not a colour";

            Assert.Equal("#00FF00", dialog.Hex);

            dialog.ColorBoxText = "red";

            Assert.True(dialog.Color.IsByAci);
            Assert.Equal(1, dialog.Color.ColorIndex);
            Assert.Equal("Red", dialog.ColorBoxText);

            dialog.ColorBoxText = "ByBlock";

            Assert.True(dialog.Color.IsByBlock);

            dialog.ColorBoxText = "300";

            Assert.True(dialog.Color.IsByBlock);
        }

        [Fact]
        public void SelectColorOfTheColorRowOfPropertiesGivesTheSelectionTheColourChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out string bottom);
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel color = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Color");
                Assert.Contains(MainViewModel.SelectColorOption, color.Options);
                bool offered = false;
                model.PickColor = dialog =>
                {
                    offered = dialog.OffersByLayerAndByBlock && dialog.Original.IsByLayer;
                    dialog.Red = 10;
                    dialog.Green = 20;
                    dialog.Blue = 30;
                    return true;
                };

                color.Value = MainViewModel.SelectColorOption;

                Assert.True(offered);
                DxfEntity line = model.Document.FindEntity(bottom);
                Assert.True(line.Color.IsByColor);
                Assert.Equal(0x0A141E, line.Color.ColorValue);
                Assert.Equal("10,20,30", model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Color").Value);
            }
        }

        [Fact]
        public void F12SwitchesDynamicInput_TheSettingsInUseFollow_AndASavedFileKeepsIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                Assert.True(model.DynamicInput.IsOn);
                Assert.Equal("DYN", model.DynamicInput.Name);

                Assert.True(model.RunKey("F12"));

                Assert.False(model.DynamicInput.IsOn);
                Assert.False(model.Settings.DynamicInput);

                // DYN typed, as the other toggles are.
                model.Execute("DYN");
                model.Execute("ON");

                Assert.True(model.DynamicInput.IsOn);

                // Saved and read back; applied, the toggle takes the value read.
                var settings = new GlobalSettings { DynamicInput = false };
                string path = Path.Combine(folder.FullPath, "settings.json");
                settings.Save(path);
                GlobalSettings read = GlobalSettings.Load(path);
                Assert.False(read.DynamicInput);

                model.ApplySettings(read);

                Assert.False(model.DynamicInput.IsOn);

                // Global settings › Canvas and cursor › Input shows it, and OK applies it.
                GlobalSettingsViewModel options = model.CreateGlobalSettings("Canvas and cursor");
                GlobalSettingRow row = options.Sections.SelectMany(s => s.Rows).Single(r => r.Label == "Dynamic input at the cursor (DYN, F12)");
                Assert.False(row.IsOn);
                row.IsOn = true;
                options.ApplyCommand.Execute(null);

                Assert.True(model.DynamicInput.IsOn);
            }
        }

        [Fact]
        public void DynamicInputShowsTheLinePromptsWithTheCoordinates_ThenTheDistanceAndAngle_ThenWhatIsTyped()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);
                model.SetCursor(1234.5, 400);

                // No command: nothing is shown.
                Assert.False(model.IsDynamicInputShown);
                Assert.Equal(string.Empty, model.DynamicInputPrompt);

                var raised = new List<string>();
                model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
                model.Execute("LINE");

                Assert.True(model.IsDynamicInputShown);
                Assert.Equal("Specify first point:", model.DynamicInputPrompt);
                Assert.Equal("1 234.50, 400.00", model.DynamicInputValue);
                Assert.Contains(nameof(MainViewModel.DynamicInputPrompt), raised);

                model.Execute("1000,1000");
                model.SetCursor(1300, 1400);

                // The box words it as AutoCAD's tooltip: the options are left to the ↓ menu.
                Assert.Equal("Specify next point or", model.DynamicInputPrompt);
                Assert.Equal("500.00 < 53.1°", model.DynamicInputValue);

                model.SetCursor(1000, 500);

                Assert.Equal("500.00 < 270.0°", model.DynamicInputValue);

                // Digits typed show in the box in place of the distance and angle.
                model.CommandText = "@300<0";

                Assert.Equal("@300<0", model.DynamicInputValue);

                model.SubmitCommand();

                Assert.Equal("Specify next point or", model.DynamicInputPrompt);

                // DYN off: nothing is shown while the command runs.
                model.DynamicInput.IsOn = false;

                Assert.False(model.IsDynamicInputShown);
                Assert.Equal(string.Empty, model.DynamicInputValue);

                model.DynamicInput.IsOn = true;
                model.Escape();

                Assert.False(model.IsDynamicInputShown);
            }
        }

        [Fact]
        public void CtrlNineHidesTheCommandLineAndShowsItAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                Assert.True(model.IsCommandLineShown);
                Assert.True(model.RunKey("Ctrl+9"));
                Assert.False(model.IsCommandLineShown);

                // Hidden, the command line still takes what is typed.
                model.CommandText = "LINE";
                model.SubmitCommand();

                Assert.Equal("Specify first point:", model.DynamicInputPrompt);

                Assert.True(model.RunKey("Ctrl+9"));
                Assert.True(model.IsCommandLineShown);
            }
        }
    }
}
