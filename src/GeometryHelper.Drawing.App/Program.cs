using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Drawing.Wpf;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;

namespace GeometryHelper.Drawing.App
{
    /// <summary>
    /// Starts GeometryHelper Drawing. <c>GeometryHelper.Drawing.App.exe plan.dxf</c> opens the window on a drawing;
    /// <c>--screenshot out.png plan.dxf [--size 1600 960] [--space "Layout1"] [--select H1,H2] [--run "MOVE;0,0"] [--cursor x,y]
    /// [--layers]</c> draws the window with the drawing open into a PNG, with no window shown, and exits: 0 when written, 2
    /// for arguments or a file it cannot read.
    /// <c>--run</c> types lines on the command line, parted by semicolons; <c>--cursor</c> puts the cursor on a point of the
    /// drawing, showing the snap and the rubber band of the prompt waiting; <c>--toggle</c> switches status toggles, as GRID;
    /// <c>--layers</c> draws the Layer manager instead.
    /// <c>--new-drawing "Test bodies" Assembly</c> makes a model drawing from a source with a preset and shows it, as New drawing
    /// does; <c>--tab Drawing</c> shows the Drawing ribbon tab; <c>--pick view|dimension|mark|line</c> selects the first entity of
    /// the drawing that draws one; <c>--hand-edit</c> moves its first dimension by hand; <c>--dialog new|new-source|new-settings|update|
    /// settings[:Tab]|export|message|run-plugin</c> draws that dialog of the model drawing instead of the window; <c>--plugin path.dll</c>
    /// loads a command plugin or a drawing plugin, <c>--tab Plugins</c> shows its tab, <c>--dialog plugins|plugin-settings|plugin-untrusted</c> draws the Plugin manager,
    /// the plugin's settings or the question before an untrusted load; <c>--dialog options</c> or <c>--dialog "settings:Canvas and
    /// cursor"</c> draws Global settings on a category; <c>--dialog color</c> or <c>--dialog truecolor</c> draws Select Color on
    /// index 30, on its Index Color or True Color tab (<c>truecolor-rgb</c> on its RGB model); <c>--dialog dsettings</c> or <c>--dialog "dsettings:Snap and Grid"</c> draws
    /// Drafting Settings on a tab; <c>--dialog osnap-menu</c> the OSNAP menu of the status bar, <c>--dialog snap-menu</c> the
    /// Shift+right-click menu of the point prompt <c>--run</c> leaves, and <c>--dialog drawings-menu</c> the right-click menu of
    /// the first row of the DRAWINGS list; <c>--dialog linetype|linetype-load|select-linetype|hatch|hatch-gradient|hatch-user|hatch-solid|hatch-file</c>
    /// draws the Linetype Manager, Load or Reload Linetypes, Select Linetype or the Hatch dialog, the lines of --run typed first;
    /// <c>--dialog missing-shx|missing-shx-replace</c> draws Missing SHX files for the drawing opened, or for a drawing of
    /// styles whose SHX files are missing where it has none; <c>--dialog ptype</c> draws Point Style, the lines of --run typed
    /// first (<c>--run "PDMODE;34"</c>).
    /// </summary>
    public static class Program
    {
        /// <summary>The entry point.</summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            args = args ?? Array.Empty<string>();

            // The code pages of the big fonts (932, 936, 949, 950) and of old DXF files, which .NET (Core) reads only once registered.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (args.Length > 0 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                return Screenshot(args);
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var model = new MainViewModel();
            var window = new MainWindow(model);
            DrawingWorkspace drawings = window.WorkspaceView.AddModelDrawings(model, DrawingWorkspaceExtensions.DefaultSources(Tolerance.Default), Tolerance.Default);

            // Global settings: read from the user's file, a value that does not read falling back to its default; applied at once.
            model.SettingsPath = GlobalSettings.DefaultPath;
            model.ApplySettings(GlobalSettings.Load(model.SettingsPath));

            // DYN switched (F12, the status bar, DYN typed) is kept in the settings file at once, as AutoCAD keeps DYNMODE.
            model.DynamicInput.PropertyChanged += (s, e) =>
            {
                try
                {
                    model.Settings.Save(model.SettingsPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn("Program: the settings could not be saved after DYN was switched.", ex);
                }
            };

            // The object snaps ticked in the OSNAP menu or Drafting Settings are kept too, as AutoCAD keeps OSMODE.
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(MainViewModel.SnapModes))
                {
                    return;
                }

                try
                {
                    model.Settings.Save(model.SettingsPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn("Program: the settings could not be saved after the object snaps changed.", ex);
                }
            };

            // The command plugins: those installed and on the startup list loaded now, each result a line of the command line.
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var plugins = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(appData, "GeometryHelper", "Drawing", "Plugins"),
                Path.Combine(appData, "GeometryHelper", "Drawing", "plugins.json"));
            plugins.ConfirmUntrusted = path => CadQuestionWindow.ConfirmUntrustedPlugin(window, path);

            // The drawing plugins are loaded by the same manager, found before the plugins of the start are loaded.
            drawings.Plugins = new DrawingPluginCatalog(plugins);
            model.UsePlugins(plugins, loadAtStart: true);
            string file = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

            if (file != null)
            {
                model.Open(file);
            }

            try
            {
                return app.Run(window);
            }
            finally
            {
                plugins.Dispose();
            }
        }

        /// <summary>Draws the window with a drawing open into a PNG, with no window shown.</summary>
        private static int Screenshot(string[] args)
        {
            string output = null;
            string file = null;
            string spaceName = null;
            string run = null;
            double[] cursor = null;
            bool layers = false;
            string sourceName = null;
            string preset = null;
            string tab = null;
            string pick = null;
            string dialogName = null;
            bool handEdit = false;
            string pluginPath = null;
            var toggles = new List<string>();
            var select = new List<string>();
            int width = 1600;
            int height = 960;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "--size", StringComparison.OrdinalIgnoreCase) && i + 2 < args.Length)
                {
                    width = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    height = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (string.Equals(arg, "--space", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    spaceName = args[++i];
                }
                else if (string.Equals(arg, "--select", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    select.AddRange(args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(h => h.Trim()));
                }
                else if (string.Equals(arg, "--run", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    run = args[++i];
                }
                else if (string.Equals(arg, "--cursor", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cursor = args[++i].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                }
                else if (string.Equals(arg, "--toggle", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    toggles.AddRange(args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpperInvariant()));
                }
                else if (string.Equals(arg, "--layers", StringComparison.OrdinalIgnoreCase))
                {
                    layers = true;
                }
                else if (string.Equals(arg, "--new-drawing", StringComparison.OrdinalIgnoreCase) && i + 2 < args.Length)
                {
                    sourceName = args[++i];
                    preset = args[++i];
                }
                else if (string.Equals(arg, "--tab", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    tab = args[++i];
                }
                else if (string.Equals(arg, "--pick", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    pick = args[++i];
                }
                else if (string.Equals(arg, "--dialog", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dialogName = args[++i];
                }
                else if (string.Equals(arg, "--plugin", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    pluginPath = Path.GetFullPath(args[++i]);
                }
                else if (string.Equals(arg, "--hand-edit", StringComparison.OrdinalIgnoreCase))
                {
                    handEdit = true;
                }
                else if (output == null)
                {
                    output = arg;
                }
                else if (file == null)
                {
                    file = arg;
                }
            }

            if (output == null || width < 100 || height < 100 || (cursor != null && cursor.Length != 2))
            {
                Console.Error.WriteLine("Usage: GeometryHelper.Drawing.App --screenshot out.png [file.dxf] [--size 1600 960] [--space \"Layout1\"] [--select H1,H2] [--run \"MOVE;0,0\"] [--cursor x,y] [--toggle GRID,SNAP] [--layers] [--new-drawing \"Test bodies\" Assembly] [--tab Drawing] [--pick view|dimension|mark|line] [--hand-edit] [--dialog new|new-source|new-settings|update|settings[:Tab]|export|message|run-plugin|plugins|plugin-settings|plugin-untrusted|color|truecolor|dsettings[:Tab]|osnap-menu|snap-menu|drawings-menu|missing-shx|missing-shx-replace|ptype] [--plugin path.dll]");
                return 2;
            }

            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var model = new MainViewModel();

            // The model drawing, its dialogs not hooked: a dialog asked for is drawn below, not shown.
            var drawings = new DrawingWorkspace(model, DrawingWorkspaceExtensions.DefaultSources(Tolerance.Default), Tolerance.Default);
            drawings.TabContent = new DrawingRibbonTab { DataContext = drawings };
            ModelDrawing made = null;

            // Command plugins and drawing plugins, kept in a folder of this run under the temporary folder, every file trusted.
            if (pluginPath != null || (dialogName ?? string.Empty).StartsWith("plugin", StringComparison.OrdinalIgnoreCase))
            {
                string pluginRoot = Path.Combine(Path.GetTempPath(), "GeometryHelper", "ScreenshotPlugins", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                var plugins = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(pluginRoot, "plugins"), Path.Combine(pluginRoot, "plugins.json"), Path.Combine(pluginRoot, "copies"))
                {
                    ConfirmUntrusted = path => true,
                };
                model.UsePlugins(plugins, loadAtStart: false);
                drawings.Plugins = new DrawingPluginCatalog(plugins);

                if (pluginPath != null && model.LoadPlugin(pluginPath)?.Succeeded != true)
                {
                    Console.Error.WriteLine(model.History.Last());
                    return 2;
                }
            }

            if (sourceName != null)
            {
                made = MakeDrawing(drawings, sourceName, preset);

                if (made == null)
                {
                    return 2;
                }

                if (handEdit && !HandEdit(made))
                {
                    Console.Error.WriteLine("The drawing has no dimension to move.");
                    return 2;
                }
            }

            foreach (StatusToggleViewModel toggle in model.Toggles.Concat(new[] { model.DynamicInput }).Where(t => toggles.Contains(t.Name)))
            {
                toggle.IsOn = !toggle.IsOn;
            }

            if (file != null && !model.Open(file))
            {
                Console.Error.WriteLine(model.History.Last());
                return 2;
            }

            if (spaceName != null)
            {
                CadSpace space = model.Document?.Spaces.FirstOrDefault(s => string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));

                if (space == null)
                {
                    Console.Error.WriteLine("The drawing has no space named " + spaceName + ".");
                    return 2;
                }

                model.SelectSpace(space);
            }

            FrameworkElement root;
            Window window;

            if (layers)
            {
                LayerManagerViewModel manager = model.CreateLayerManager();

                if (manager == null)
                {
                    Console.Error.WriteLine("--layers needs a drawing.");
                    return 2;
                }

                var dialog = new LayerManagerWindow(manager);
                manager.SelectedRow = manager.Rows.FirstOrDefault(r => r.IsCurrent);
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (string.Equals(dialogName, "save-changes", StringComparison.OrdinalIgnoreCase))
            {
                // The question CLOSE asks of a drawing with changes not saved.
                var question = CadQuestionWindow.ForSave(new[] { made?.Document ?? CadDocument.From(new DxfDatabase("AC1027"), "plan-level2.dxf") });
                root = question.Body;
                window = question;
                width = (int)question.Width;
                height = MeasuredHeight(root, width);
            }
            else if (string.Equals(dialogName, "color", StringComparison.OrdinalIgnoreCase) || (dialogName ?? string.Empty).StartsWith("truecolor", StringComparison.OrdinalIgnoreCase))
            {
                // Select Color on index 30, as the Layer manager shows it for a layer's colour cell; truecolor-rgb on the RGB model.
                var dialog = new SelectColorWindow(new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30), true));
                dialog.Model.IsTrueColorTab = dialogName.StartsWith("truecolor", StringComparison.OrdinalIgnoreCase);
                dialog.Model.ColorModel = string.Equals(dialogName, "truecolor-rgb", StringComparison.OrdinalIgnoreCase) ? "RGB" : "HSL";
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (dialogName != null && dialogName.StartsWith("dsettings", StringComparison.OrdinalIgnoreCase))
            {
                // Drafting Settings on a tab, as DSETTINGS shows it: --dialog dsettings, or --dialog "dsettings:Snap and Grid".
                int colon = dialogName.IndexOf(':');
                var dialog = new DraftingSettingsWindow(model.CreateDraftingSettings(colon < 0 ? null : dialogName.Substring(colon + 1)));
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (dialogName != null && dialogName.EndsWith("-menu", StringComparison.OrdinalIgnoreCase))
            {
                // A menu of the window, drawn as it opens: the lines of --run typed first, for the menu of a point prompt.
                // Each part is one line of the command line; an empty part is Enter.
                foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
                {
                    model.Execute(line);
                }

                run = null;
                FileRowViewModel row = model.FileRows.FirstOrDefault(r => !r.IsHeader);

                if (row != null)
                {
                    model.SelectFileRow(row);
                }

                IReadOnlyList<CadMenuItemViewModel> items = string.Equals(dialogName, "osnap-menu", StringComparison.OrdinalIgnoreCase) ? model.OsnapMenu()
                    : string.Equals(dialogName, "snap-menu", StringComparison.OrdinalIgnoreCase) ? model.SnapOverrideMenu()
                    : string.Equals(dialogName, "drawings-menu", StringComparison.OrdinalIgnoreCase) ? model.FileRowMenu(row)
                    : Array.Empty<CadMenuItemViewModel>();

                if (items.Count == 0)
                {
                    Console.Error.WriteLine("--dialog " + dialogName + ": no such menu, or nothing to show: snap-menu needs a point asked (--run \"LINE\"), drawings-menu a drawing.");
                    return 2;
                }

                root = CadMenus.Make(items);
                window = new Window();
                width = 260;
                height = MeasuredHeight(root, width);
            }
            else if (dialogName != null && dialogName.StartsWith("missing-shx", StringComparison.OrdinalIgnoreCase))
            {
                // Missing SHX files for the drawing opened, or for a drawing of three styles whose SHX files are missing;
                // missing-shx-replace on Specify a replacement.
                CadDocument document = model.Document;
                IReadOnlyList<CadMissingFont> missing = document?.FindMissingFonts() ?? Array.Empty<CadMissingFont>();

                if (missing.Count == 0)
                {
                    var database = new DxfDatabase("AC1027");
                    database.TextStyleTable["Standard"].FileName = CadFonts.BuiltInName + ".shx";
                    database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "HZ", FileName = CadFonts.BuiltInName + ".shx", BigFontFileName = "hztxt.shx" });
                    database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Notes-CN", FileName = CadFonts.BuiltInName + ".shx", BigFontFileName = "hztxt.shx" });
                    database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Dims", FileName = "romand.shx" });
                    database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "TITLE", FileName = "romand.shx" });
                    database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "TSSD_Note", FileName = "tssdeng.shx" });
                    document = CadDocument.From(database, "plan-level2.dxf");
                    document.Fonts = new GlobalSettings().ToFonts();
                    missing = document.FindMissingFonts();
                }

                var dialog = new MissingShxWindow(new MissingShxViewModel(document, missing));
                dialog.Model.Replace = dialogName.EndsWith("-replace", StringComparison.OrdinalIgnoreCase);
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (string.Equals(dialogName, "ptype", StringComparison.OrdinalIgnoreCase))
            {
                // Point Style, as PTYPE shows it, for the drawing opened or a new one, the lines of --run typed first.
                foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
                {
                    model.Execute(line);
                }

                run = null;
                var dialog = new PointStyleWindow(model.CreatePointStyle() ?? new PointStyleViewModel(CadDocument.From(new DxfDatabase("AC1027"), "plan-level2.dxf")));
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (model.Document != null && LinetypeOrHatchDialog(model, dialogName, ref run, out window, out root))
            {
                // The Linetype Manager, its Load dialog, Select Linetype or the Hatch dialog, after the lines of --run.
                width = (int)window.Width;
                height = (int)window.Height;
            }
            else if (model.Document != null && StyleDialog(model, dialogName, ref run, out window, out root))
            {
                // The Text Style dialog, the Dimension Style Manager and its dialogs, after the lines of --run.
                width = (int)window.Width;
                height = double.IsNaN(window.Height) ? MeasuredHeight(root, width) : (int)window.Height;
            }
            else if (dialogName != null && GlobalCategory(dialogName, out string category))
            {
                // Global settings on a category, as OPTIONS shows it: --dialog options, or --dialog "settings:Canvas and cursor".
                var dialog = new GlobalSettingsWindow(model.CreateGlobalSettings(category));
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else if (dialogName != null && dialogName.StartsWith("plugin", StringComparison.OrdinalIgnoreCase))
            {
                // The Plugin manager, the settings of the plugin loaded, or the question asked before an untrusted load.
                PluginManagerViewModel manager = model.CreatePluginManager();

                if (string.Equals(dialogName, "plugin-untrusted", StringComparison.OrdinalIgnoreCase))
                {
                    var question = new CadQuestionWindow("Load a plugin from a folder that is not trusted?",
                        "Loading a plugin runs its code with your rights. Load it only if you know where it comes from; to stop being asked, add its folder to the trusted folders in the Plugin manager.",
                        pluginPath ?? @"D:\Dev\CircleTools\bin\Debug\CircleTools.dll", "Load", "Don't load");
                    root = question.Body;
                    window = question;
                    width = (int)question.Width;
                    height = MeasuredHeight(root, width);
                }
                else if (string.Equals(dialogName, "plugin-settings", StringComparison.OrdinalIgnoreCase) && manager?.CreateSettings() is PluginSettingsViewModel settings)
                {
                    var dialog = new PluginSettingsWindow(settings);
                    root = dialog.Body;
                    window = dialog;
                    width = (int)dialog.Width;
                    height = MeasuredHeight(root, width);
                }
                else if (manager != null)
                {
                    var dialog = new PluginManagerWindow(manager);
                    root = dialog.Body;
                    window = dialog;
                    width = (int)dialog.Width;
                    height = (int)dialog.Height;
                }
                else
                {
                    Console.Error.WriteLine("--dialog " + dialogName + ": needs --plugin.");
                    return 2;
                }
            }
            else if (dialogName != null)
            {
                DialogViewModel dialog = Dialog(drawings, dialogName);

                if (dialog == null)
                {
                    Console.Error.WriteLine("--dialog " + dialogName + ": no such dialog, or it needs --new-drawing first.");
                    return 2;
                }

                var dialogWindow = DrawingWorkspaceExtensions.CreateDialog(dialog);
                root = dialogWindow.Body;
                window = dialogWindow;
                width = (int)dialogWindow.Width;
                height = double.IsNaN(dialogWindow.Height) ? MeasuredHeight(root, width) : (int)dialogWindow.Height;
            }
            else
            {
                window = new MainWindow(model);
                model.ApplySettings(new GlobalSettings());
                root = (FrameworkElement)window.Content;
            }

            window.Content = null;
            object context = layers ? (object)((LayerManagerWindow)window).Model : dialogName != null ? window.DataContext : model;
            root.DataContext = context;

            // A dialog sized to its content is measured again out of its window, its rows bound now.
            if (dialogName != null && window.SizeToContent != SizeToContent.Manual)
            {
                height = MeasuredHeight(root, width);
            }

            Layout(root, width, height);

            if (tab != null)
            {
                model.SelectedTab = string.Equals(tab, "Plugins", StringComparison.OrdinalIgnoreCase) ? model.PluginsTab
                    : string.Equals(tab, "Settings", StringComparison.OrdinalIgnoreCase) ? model.SettingsTab
                    : string.Equals(tab, "Annotate", StringComparison.OrdinalIgnoreCase) ? model.AnnotateTab
                    : string.Equals(tab, "View", StringComparison.OrdinalIgnoreCase) ? model.ViewTab
                    : model.Extensions.FirstOrDefault(e => string.Equals(e.TabHeader, tab, StringComparison.OrdinalIgnoreCase));
                Layout(root, width, height);
            }

            if (pick != null && made != null)
            {
                string handle = Pick(made, pick);

                if (handle == null)
                {
                    Console.Error.WriteLine("--pick " + pick + ": the drawing has no such entity in the space shown.");
                    return 2;
                }

                made.Document.Selection.Set(new[] { handle });
                Layout(root, width, height);
            }

            if (select.Count > 0 && model.Document != null)
            {
                model.Document.Selection.Set(select);
                Layout(root, width, height);
            }

            if (run != null)
            {
                // Each part is one line of the command line; an empty part is Enter.
                foreach (string line in run.Split(';'))
                {
                    model.Execute(line);
                }

                Layout(root, width, height);
            }

            if (cursor != null && root is CadWorkspace workspace)
            {
                workspace.DrawingCanvas.MoveCursorTo(cursor[0], cursor[1]);
                Layout(root, width, height);
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string folder = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(folder);

            using (FileStream stream = File.Create(output))
            {
                encoder.Save(stream);
            }

            // A drawing --run changed is not asked about: the question is a dialog, which nobody answers here.
            model.AskSaveAll = documents => CadSaveChoice.DontSave;
            window.Close();
            Console.WriteLine("Wrote " + Path.GetFullPath(output) + " (" + width + " by " + height + ").");
            return 0;
        }

        /// <summary>Makes a model drawing through the New drawing dialog's steps: the source named, the preset, Create.</summary>
        private static ModelDrawing MakeDrawing(DrawingWorkspace drawings, string sourceName, string preset)
        {
            NewDrawingViewModel dialog = drawings.ShowNewDrawing();
            SourceOption source = dialog.Sources.FirstOrDefault(s => string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            if (source == null || !source.IsAvailable)
            {
                Console.Error.WriteLine("--new-drawing: no source named " + sourceName + " can give objects; there are " + string.Join(", ", dialog.Sources.Select(s => s.Name)) + ".");
                return null;
            }

            source.IsSelected = true;
            Pump(dialog.NextAsync());

            if (!NewDrawingViewModel.PresetNames.Contains(preset, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("--new-drawing: the preset is Part, Assembly or General.");
                return null;
            }

            dialog.Preset = NewDrawingViewModel.PresetNames.First(p => string.Equals(p, preset, StringComparison.OrdinalIgnoreCase));
            Pump(dialog.NextAsync());
            ModelDrawing made = Pump(dialog.NextAsync());

            if (made == null)
            {
                Console.Error.WriteLine("--new-drawing: the drawing was not made. " + dialog.Notice);
            }

            return made;
        }

        /// <summary>Moves the first dimension of a drawing 25 up by hand, through its owner, as a grip drag does.</summary>
        private static bool HandEdit(ModelDrawing made)
        {
            string handle = Pick(made, "dimension", anySpace: true);
            return handle != null && made.Document.Owners.TryApply(made.Document, new CadEdit(handle, CadEditKind.Move, null, null, "0,25"));
        }

        /// <summary>The view model of a dialog of the model drawing, at the step or tab named after a colon.</summary>
        private static DialogViewModel Dialog(DrawingWorkspace drawings, string name)
        {
            string[] parts = name.Split(':');

            switch (parts[0].ToLowerInvariant())
            {
                case "new":
                case "new-objects":
                    NewDrawingViewModel objects = drawings.ShowNewDrawing();
                    Pump(objects.NextAsync());
                    return objects;
                case "new-source":
                    return drawings.ShowNewDrawing();
                case "new-settings":
                    NewDrawingViewModel settings = drawings.ShowNewDrawing();
                    Pump(settings.NextAsync());
                    Pump(settings.NextAsync());
                    return settings;
                case "update":
                    UpdateFromModelViewModel update = drawings.ShowUpdate();

                    if (update != null)
                    {
                        update.Source = drawings.Providers.FirstOrDefault(p => p.IsAvailable && p.Name.EndsWith("changed", StringComparison.Ordinal)) ?? update.Source;
                        Pump(update.PreviewAsync());
                    }

                    return update;
                case "settings":
                    DrawingSettingsViewModel tabs = drawings.ShowSettings();

                    if (tabs != null && parts.Length > 1)
                    {
                        tabs.Tab = parts[1];
                    }

                    return tabs;
                case "export":
                    return drawings.ShowExport();
                case "message":
                    return MessageViewModel.Failure("Drawing not made", "B-105 could not be made from 4 parts.", new ArgumentException("The settings allow no scale."));
                case "run-plugin":
                    return drawings.ShowRunPlugin();
                default:
                    return null;
            }
        }

        /// <summary>The first entity of a drawing, in the space shown, that draws a view, a dimension, a mark or a line of the model.</summary>
        private static string Pick(ModelDrawing made, string kind, bool anySpace = false)
        {
            var kinds = made.Drawing.Elements.GroupBy(e => e.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Kind, StringComparer.Ordinal);

            foreach (DxfEntity entity in DrawingEntityTags.Find(made.Document.Database))
            {
                if (entity.IsErased || !DrawingEntityTags.TryRead(entity, out string id, out _)
                    || (!anySpace && made.Document.FindSpace(entity.Handle) != made.Document.CurrentSpace))
                {
                    continue;
                }

                bool line = !kinds.ContainsKey(id) && entity is DxfLine;

                if (string.Equals(kind, "line", StringComparison.OrdinalIgnoreCase) ? line : kinds.TryGetValue(id, out string found) && string.Equals(found, kind, StringComparison.OrdinalIgnoreCase))
                {
                    return entity.Handle;
                }
            }

            return null;
        }

        /// <summary>
        /// The dialogs of C9 a --dialog names, the lines of --run typed first: linetype (the Linetype Manager, its details shown,
        /// the presenter's own linetypes waiting to be loaded), linetype-load (Load or Reload Linetypes on acadiso.lin or the
        /// presenter's own set), select-linetype (Select Linetype), hatch (the Hatch dialog on ANSI31), hatch-gradient (on a
        /// gradient), hatch-user (on lines of the user's, doubled), hatch-solid (on a solid fill) and hatch-file (a .pat file of
        /// the user's chosen, a pattern of it shown). False for any other name.
        /// </summary>
        private static bool LinetypeOrHatchDialog(MainViewModel model, string dialogName, ref string run, out Window window, out FrameworkElement root)
        {
            window = null;
            root = null;
            string name = (dialogName ?? string.Empty).ToLowerInvariant();

            if (name != "linetype" && name != "linetype-load" && name != "select-linetype" && !name.StartsWith("hatch", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;

            switch (name)
            {
                case "linetype":
                    LinetypeManagerViewModel manager = model.CreateLinetypeManager();
                    manager.Add(CadLinetypeFile.BuiltIn.Where(l => l.Name == "DASHDOT" || l.Name == "GAS_LINE"), CadLinetypeFile.BuiltInName);
                    manager.SelectedRow = manager.Rows.FirstOrDefault(r => r.Name == "DASHDOT");
                    manager.ShowDetails = true;
                    var managerWindow = new LinetypeManagerWindow(manager);
                    window = managerWindow;
                    root = managerWindow.Body;
                    break;
                case "linetype-load":
                    var load = new LoadLinetypesViewModel(model.Document);

                    foreach (LoadLinetypeRowViewModel row in load.Rows.Take(3))
                    {
                        row.IsChosen = true;
                    }

                    var loadWindow = new LoadLinetypesWindow(load);
                    window = loadWindow;
                    root = loadWindow.Body;
                    break;
                case "select-linetype":
                    var select = new SelectLinetypeViewModel(model.Document, model.Document.Database.Celtype);
                    var selectWindow = new SelectLinetypeWindow(select);
                    window = selectWindow;
                    root = selectWindow.Body;
                    break;
                default:
                    CadHatchSettings settings = model.Document.HatchSettings.Clone();
                    settings.Type = name == "hatch-gradient" ? CadHatchType.Gradient : name == "hatch-user" ? CadHatchType.UserDefined : name == "hatch-solid" ? CadHatchType.Solid : settings.Type;
                    settings.Double = name == "hatch-user";
                    settings.Spacing = 2.0;
                    settings.Angle = name == "hatch-user" ? 30.0 : settings.Angle;
                    var hatch = new HatchSettingsViewModel(model.Document, settings, false);

                    // hatch-file: a .pat file of six patterns chosen by Browse…, GRATING-30 of it chosen.
                    if (name == "hatch-file")
                    {
                        string folder = Path.Combine(Path.GetTempPath(), "GeometryHelper", "ScreenshotPatterns");
                        Directory.CreateDirectory(folder);
                        string pat = Path.Combine(folder, "steel-works.pat");
                        File.WriteAllText(pat, "*GRATING-30,Grating at 30 degrees\n30, 0,0, 0,5\n120, 0,0, 0,5\n*CHEQUER,Chequer plate\n45, 0,0, 0,6, 3,-3\n135, 0,0, 0,6, 3,-3\n"
                            + "*MESH-50,Mesh 50\n0, 0,0, 0,50\n90, 0,0, 0,50\n*BOLTS,Bolt pattern\n0, 0,0, 10,10, 0,-10\n*FLOOR-PL,Floor plate\n60, 0,0, 0,8, 4,-4\n"
                            + "*ANSI31,Not the built-in one\n45, 0,0, 0,1\n*WELD,Weld\n45, 0,0, 0,2, 1,-1\n");
                        hatch.PatternFile = pat;
                        hatch.Pattern = hatch.Patterns.First(p => p.Name == "GRATING-30");
                    }

                    var hatchWindow = new HatchSettingsWindow(hatch);
                    window = hatchWindow;
                    root = hatchWindow.Body;
                    break;
            }

            return true;
        }

        /// <summary>Whether a --dialog names Global settings: options, or settings: and one of its categories, given back.</summary>
        private static bool GlobalCategory(string dialogName, out string category)
        {
            string[] categories = { "Appearance", "Canvas and cursor", "Drawing defaults", "Units and rounding", "Layers, colours and lines", "Text and fonts", "Fonts", "Data sources", "Shortcuts", "Save and export" };
            category = null;

            if (string.Equals(dialogName, "options", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!dialogName.StartsWith("settings:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            category = categories.FirstOrDefault(c => string.Equals(c, dialogName.Substring("settings:".Length), StringComparison.OrdinalIgnoreCase));
            return category != null;
        }

        /// <summary>
        /// The style dialogs (C10), after the lines of --run: <c>style</c> or <c>textstyle</c> (Text Style), <c>style-new</c> (its
        /// name asked), <c>dimstyle</c> (the Manager), <c>dimstyle-new</c> (Create New Dimension Style), <c>dimstyle-modify[:Tab]</c>
        /// (or <c>dimstyle-edit[:Tab]</c>) and
        /// <c>dimstyle-override[:Tab]</c> of the style selected or current, <c>dimstyle-compare[:Other]</c>.
        /// </summary>
        private static bool StyleDialog(MainViewModel model, string dialogName, ref string run, out Window window, out FrameworkElement root)
        {
            window = null;
            root = null;
            string[] parts = (dialogName ?? string.Empty).Split(':');
            string kind = parts[0].ToLowerInvariant();
            kind = kind == "textstyle" ? "style" : kind == "dimstyle-edit" ? "dimstyle-modify" : kind;

            if (kind != "style" && kind != "style-new" && !kind.StartsWith("dimstyle", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;
            DimStyleManagerViewModel manager = model.CreateDimStyleManager();

            switch (kind)
            {
                case "style":
                    var text = new TextStyleWindow(model.CreateTextStyle());
                    (window, root) = (text, text.Body);
                    return true;
                case "style-new":
                    var name = new StyleNameWindow(new StyleNameViewModel("New Text Style", "style1", null));
                    (window, root) = (name, name.Body);
                    return true;
                case "dimstyle":
                    var dialog = new DimStyleManagerWindow(manager);
                    (window, root) = (dialog, dialog.Body);
                    return true;
                case "dimstyle-new":
                    string start = manager.SelectedStyle?.Name;
                    var ask = new StyleNameWindow(new StyleNameViewModel("Create New Dimension Style", "Copy of " + start, null, manager.Styles.Select(s => s.Name).ToList(), start));
                    (window, root) = (ask, ask.Body);
                    return true;
                case "dimstyle-modify":
                case "dimstyle-override":
                    DimStyleEditViewModel editor = kind == "dimstyle-modify" ? manager.CreateModify() : manager.CreateOverride();

                    if (parts.Length > 1)
                    {
                        editor.Tab = editor.Tabs.FirstOrDefault(t => string.Equals(t, parts[1], StringComparison.OrdinalIgnoreCase)) ?? editor.Tab;
                    }

                    var edit = new DimStyleEditWindow(editor);
                    (window, root) = (edit, edit.Body);
                    return true;
                case "dimstyle-compare":
                    DimStyleCompareViewModel compare = manager.CreateCompare();

                    if (parts.Length > 1)
                    {
                        compare.Second = parts[1];
                    }

                    var against = new DimStyleCompareWindow(compare);
                    (window, root) = (against, against.Body);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Waits for a task while the dispatcher runs, so that its awaits come back to this thread.</summary>
        private static T Pump<T>(Task<T> task)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            return task.GetAwaiter().GetResult();
        }

        /// <summary>The height a dialog sized to its content takes at a width.</summary>
        private static int MeasuredHeight(FrameworkElement root, int width)
        {
            root.Measure(new Size(width, double.PositiveInfinity));
            return (int)Math.Ceiling(root.DesiredSize.Height);
        }

        /// <summary>Lays the content out at a size and draws the canvas at once, as no frame will come to draw it.</summary>
        private static void Layout(FrameworkElement root, int width, int height)
        {
            root.Width = width;
            root.Height = height;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            if (root is CadWorkspace workspace)
            {
                workspace.DrawingCanvas.RenderNow();
                root.UpdateLayout();
            }
        }
    }
}
