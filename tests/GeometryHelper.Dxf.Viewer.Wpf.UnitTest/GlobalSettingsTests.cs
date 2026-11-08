using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Global settings: the defaults, the JSON file, a bad value, Apply to the window and the canvas, the shortcuts, the dialog.</summary>
    public sealed class GlobalSettingsTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheDefaultsAreAutoCadsAndTheDesigns()
        {
            var settings = new GlobalSettings();

            Assert.Equal(0x000000, settings.CanvasBackground);
            Assert.Equal(0xFFFFFF, settings.Paper);
            Assert.Equal(0x3D9BFF, settings.Selection);
            Assert.Equal(5, settings.PickboxPixels);
            Assert.Equal(10, settings.AperturePixels);
            Assert.Equal(1.2, settings.WheelZoomFactor);
            Assert.Equal(CadSnapModes.All & ~CadSnapModes.Nearest, settings.Snaps);
            Assert.Equal(CadShortcuts.Default.Aliases, settings.Aliases);
            Assert.Equal(CadShortcuts.Default.Keys, settings.Keys);
            Assert.Equal(new[] { "Visible", "Hidden", "Centre", "Hatch", "Dimension", "Mark", "Sheet" }, settings.Layers.Select(l => l.Name));
            Assert.Equal("Assembly", settings.DefaultPreset);
            Assert.Equal("A2", settings.DefaultSheetSize);
            Assert.Equal("LayoutAndViewports", settings.ExportLayout);
            Assert.Equal("12 345.68", settings.FormatNumber(12345.678, 2));
            Assert.EndsWith("settings.json", GlobalSettings.DefaultPath);
        }

        [Fact]
        public void AFileWrittenReadsBackTheSameSettings()
        {
            var settings = new GlobalSettings
            {
                CanvasBackground = 0x212830,
                Paper = 0xF0F0F0,
                Selection = 0xD29922,
                PickboxPixels = 7,
                WheelZoomFactor = 1.5,
                Snaps = CadSnapModes.Endpoint | CadSnapModes.Nearest,
                DefaultPreset = "Part",
                CoordinateDecimals = 3,
                DecimalSeparator = "Comma",
                ThousandsSeparator = "Point",
                DrawingFont = "osifont",
                ExportLayout = "SheetInModelSpace",
                SaveFolder = @"D:\Drawings ""A""",
                ShowLineWeights = false,
            };
            settings.Layers = settings.Layers.Concat(new[] { new GlobalLayer("Weld", 0xF85149, "Continuous", 0.5, false) }).ToList();
            settings.SetShortcuts(settings.Aliases.Concat(new[] { new KeyValuePair<string, string>("ZZ", "ZOOM") }), settings.Keys.Concat(new[] { new KeyValuePair<string, string>("Ctrl+Shift+P", "PLOT") }));
            string path = Path.Combine(_folder.FullPath, "deep", "settings.json");

            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(settings.ToJson(), read.ToJson());
            Assert.Equal(0x212830, read.CanvasBackground);
            Assert.Equal(CadSnapModes.Endpoint | CadSnapModes.Nearest, read.Snaps);
            Assert.Equal("12.345,678", read.FormatNumber(12345.678, 3));
            Assert.Equal("ZOOM", read.Aliases["zz"]);
            Assert.Equal("PLOT", read.Keys["Ctrl+Shift+P"]);
            Assert.False(read.Layers.Last().Plot);
            Assert.DoesNotContain("\r", File.ReadAllText(path));
        }

        [Fact]
        public void ABadValueFallsBackToItsDefaultWithAWarnAndTheOthersAreRead()
        {
            string path = Path.Combine(_folder.FullPath, "settings.json");
            File.WriteAllText(path, "{ \"appearance\": { \"canvasBackground\": \"blue\", \"pickboxPixels\": 500, \"paper\": \"#EEEEEE\" }, \"canvas\": { \"wheelZoomFactor\": 1.5, \"snaps\": \"Endpoint,Bogus\" }, \"units\": { \"thousandsSeparator\": \"Point\" }, \"layers\": [ { \"name\": \"\", \"colour\": \"#FFFFFF\", \"linetype\": \"x\", \"lineweight\": 0.25 }, { \"name\": \"Visible\", \"colour\": \"#FFFFFF\", \"linetype\": \"Continuous\", \"lineweight\": 0.35 } ] }");
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            GlobalSettings read;

            try
            {
                GeometryHelperLog.Writer = (level, text, exception) =>
                {
                    lock (lines)
                    {
                        lines.Add(GeometryHelperLog.Format(level, text, exception));
                    }
                };
                read = GlobalSettings.Load(path);
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            Assert.Equal(0x000000, read.CanvasBackground);
            Assert.Equal(5, read.PickboxPixels);
            Assert.Equal(0xEEEEEE, read.Paper);
            Assert.Equal(1.5, read.WheelZoomFactor);
            Assert.Equal(CadSnapModes.All & ~CadSnapModes.Nearest, read.Snaps);
            Assert.Equal("Space", read.ThousandsSeparator);
            Assert.Equal(new[] { "Visible" }, read.Layers.Select(l => l.Name));
            Assert.Contains(lines, l => l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("appearance.pickboxPixels"));
            Assert.Contains(lines, l => l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("appearance.canvasBackground"));
            Assert.Contains(lines, l => l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("canvas.snaps"));
            Assert.Contains(lines, l => l.StartsWith("WARN ", StringComparison.Ordinal) && l.Contains("units.thousandsSeparator"));

            File.WriteAllText(path, "{ this is not JSON");
            Assert.Equal(new GlobalSettings().ToJson(), GlobalSettings.Load(path).ToJson());
            Assert.Equal(new GlobalSettings().ToJson(), GlobalSettings.Load(Path.Combine(_folder.FullPath, "none.json")).ToJson());
        }

        [Fact]
        public void ApplyGivesTheCanvasItsColoursPickboxAndZoomAndTheStatusBarItsUnits()
        {
            int background = -1, paper = -1, selection = -1;
            double pickbox = 0, wheel = 0, aperture = 0;
            string font = null;
            Exception failure = null;
            var model = new MainViewModel();
            var thread = new Thread(() =>
            {
                try
                {
                    var workspace = new CadWorkspace { DataContext = model };
                    var settings = new GlobalSettings
                    {
                        CanvasBackground = 0x212830,
                        Paper = 0xF0F0F0,
                        Selection = 0xD29922,
                        PickboxPixels = 9,
                        AperturePixels = 14,
                        WheelZoomFactor = 1.5,
                        DrawingFont = "Consolas",
                        Snaps = CadSnapModes.Endpoint,
                        CoordinateDecimals = 3,
                        DecimalSeparator = "Comma",
                        ThousandsSeparator = "Point",
                        Grid = true,
                    };
                    model.ApplySettings(settings);
                    CadCanvas canvas = workspace.DrawingCanvas;
                    background = canvas.Options.Background;
                    paper = canvas.Options.Paper;
                    selection = canvas.Options.SelectionColor;
                    font = canvas.Options.FontName;
                    pickbox = canvas.PickboxPixels;
                    aperture = canvas.AperturePixels;
                    wheel = canvas.WheelZoomFactor;
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
            Assert.Equal(0x212830, background);
            Assert.Equal(0xF0F0F0, paper);
            Assert.Equal(0xD29922, selection);
            Assert.Equal("Consolas", font);
            Assert.Equal(9.0, pickbox);
            Assert.Equal(14.0, aperture);
            Assert.Equal(1.5, wheel);
            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Modes);
            Assert.True(model.GridOn);

            model.SetCursor(12345.6789, -2.5);

            Assert.Equal("X 12.345,679   Y -2,500", model.CursorText);
        }

        [Fact]
        public void AKeyGivenToTwoCommandsIsMarkedAndApplyWaitsUntilItIsPutRight()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            Assert.True(dialog.IsShortcutsPage);
            ShortcutRowViewModel move = dialog.Shortcuts.Single(r => r.Command == "MOVE");
            ShortcutRowViewModel properties = dialog.Shortcuts.Single(r => r.Command == "PROPERTIES");
            Assert.Equal("Ctrl+1", properties.Keys);
            Assert.Equal("CH, PR", properties.Aliases);
            Assert.DoesNotContain(dialog.Shortcuts, r => r.HasConflict);

            move.Keys = "Ctrl+1";

            Assert.Contains(dialog.Shortcuts, r => r.HasConflict && r.Conflict.StartsWith("Ctrl+1 is already used by ", StringComparison.Ordinal));
            Assert.True(dialog.HasErrors);
            Assert.False(dialog.ApplyCommand.CanExecute(null));

            move.Keys = string.Empty;
            move.Aliases = "M, ZOOM";
            Assert.Equal("ZOOM is the name of a command", move.Conflict);

            move.Aliases = "M";
            Assert.False(dialog.HasErrors);
            dialog.ShortcutSearch = "ctrl+1";
            Assert.Equal(new[] { "PROPERTIES" }, dialog.Shortcuts.Select(r => r.Command));
        }

        [Fact]
        public void ShortcutsAppliedReachTheCommandLineAndTheKeys()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "plain.dxf"));
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            dialog.Shortcuts.Single(r => r.Command == "ZOOM").Aliases = "Z, ZZ";
            dialog.Shortcuts.Single(r => r.Command == "REGEN").Keys = "F5";

            Assert.True(dialog.Apply());

            Assert.Equal("ZOOM", model.Session.Registry.Find("ZZ").Name);
            Assert.True(model.RunKey("F5"));
            Assert.Contains("Command: REGEN", model.History);
            Assert.False(model.RunKey("Ctrl+Alt+Q"));
            Assert.Equal("ZOOM", model.Settings.Aliases["ZZ"]);
        }

        [Fact]
        public void ARefusedValueIsSaidAndOkSavesTheFileOnceItIsRight()
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings();
            Assert.Equal("Appearance", dialog.SelectedCategory.Name);
            GlobalSettingRow pickbox = dialog.Row("Appearance", "Pickbox");

            pickbox.Value = "500";
            Assert.True(pickbox.HasError);
            Assert.False(dialog.Ok());
            Assert.False(File.Exists(model.SettingsPath));

            pickbox.Value = "8";
            dialog.Row("Appearance", "Canvas background").NamedColour = "Dark grey · #212830";
            bool? closed = null;
            dialog.CloseRequested += (s, done) => closed = done;

            Assert.True(dialog.Ok());

            Assert.True(closed);
            Assert.Equal(8, model.Settings.PickboxPixels);
            Assert.Equal(0x212830, model.Settings.CanvasBackground);
            Assert.Equal(model.Settings.ToJson(), GlobalSettings.Load(model.SettingsPath).ToJson());

            dialog.Reset();
            Assert.Equal(5, dialog.Working.PickboxPixels);
            Assert.Equal(8, model.Settings.PickboxPixels);
        }

        [Fact]
        public void TheDialogDrawsEachCategoryOffscreen()
        {
            var shots = new Dictionary<string, int>();
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    model.SettingsSources = new[] { new CadSourceInfo("Test bodies", "Built in", true), new CadSourceInfo("Tekla Structures", "Not connected", false) };

                    foreach (string category in new[] { "Appearance", "Layers, colours and lines", "Data sources", "Shortcuts" })
                    {
                        var window = new GlobalSettingsWindow(model.CreateGlobalSettings(category));
                        FrameworkElement body = window.Body;
                        window.Content = null;
                        body.DataContext = window.Model;
                        body.Measure(new Size(840, 600));
                        body.Arrange(new Rect(0, 0, 840, 600));
                        body.UpdateLayout();
                        var bitmap = new RenderTargetBitmap(840, 600, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(body);
                        var pixels = new byte[840 * 600 * 4];
                        bitmap.CopyPixels(pixels, 840 * 4, 0);
                        var colours = new HashSet<int>();

                        for (int i = 0; i < pixels.Length; i += 4)
                        {
                            colours.Add(BitConverter.ToInt32(pixels, i));
                        }

                        shots[category] = colours.Count;
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
            Assert.All(shots, s => Assert.True(s.Value > 30, s.Key + ": " + s.Value + " colours"));
        }
    }
}
