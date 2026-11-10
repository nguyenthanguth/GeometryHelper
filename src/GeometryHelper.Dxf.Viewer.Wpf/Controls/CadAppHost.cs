using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Starts a standalone app of the presenter: the window on the drawing named on the command line, or, with
    /// <c>--screenshot</c>, a picture of the window or of one of its dialogs drawn into a PNG with no window shown. GeometryHelper
    /// DXF runs it as it is; GeometryHelper Drawing derives from it to add the model drawing and its own options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>app.exe plan.dxf</c> opens the window on a drawing, reads the global settings from the user's file and loads the
    /// command plugins of <see cref="PluginFolder"/> on its startup list.
    /// </para>
    /// <para>
    /// <c>app.exe --screenshot out.png [plan.dxf] [--size 1600 960] [--space "Layout1"] [--select H1,H2] [--run "MOVE;0,0"]
    /// [--cursor x,y] [--toggle GRID,SNAP] [--layers] [--tab Insert] [--dialog name] [--plugin path.dll]</c> draws the window
    /// with the drawing open, 1 600 by 960 unless <c>--size</c> says otherwise, and exits: 0 when written, 2 for arguments or a
    /// file it cannot read. <c>--run</c> types lines on the command line, parted by semicolons, an empty part being Enter;
    /// <c>--cursor</c> puts the cursor on a point of the drawing, showing the snap and the rubber band of the prompt waiting;
    /// <c>--type REC</c> types text and leaves it unsent, the AutoComplete list shown for it, and <c>--type {Down}</c> opens
    /// the options menu of dynamic input; <c>--dialog plot:png</c>, <c>plot:jpg</c> and <c>plot-custom</c> draw PLOT's
    /// dialog on a raster device and its Custom Image Size;
    /// <c>--toggle</c> switches status toggles, as GRID or DYN; <c>--tab</c> shows a ribbon tab (Insert, Annotate, View,
    /// Plugins, Settings or the header of a tab a host adds); <c>--plugin</c> loads a command plugin, every file trusted.
    /// <c>--layers</c> draws the Layer manager instead, and <c>--dialog</c> one of the presenter's dialogs or menus:
    /// <c>save-changes</c>, <c>options</c> or <c>settings:Category</c> (Global settings), <c>plugins</c>,
    /// <c>plugin-settings</c>, <c>plugin-untrusted</c>, <c>color</c>, <c>truecolor</c>, <c>truecolor-rgb</c>,
    /// <c>dsettings[:Tab]</c>, <c>osnap-menu</c>, <c>snap-menu</c>, <c>drawings-menu</c>, <c>scale-menu</c>, <c>missing-shx</c>,
    /// <c>missing-shx-replace</c>, <c>ptype</c>, <c>linetype</c>, <c>linetype-load</c>, <c>select-linetype</c>, <c>hatch</c>,
    /// <c>hatch-gradient</c>, <c>hatch-user</c>, <c>hatch-solid</c>, <c>hatch-file</c>, <c>block</c>, <c>insert</c>,
    /// <c>wblock</c>, <c>attdef</c>, <c>eattedit[:Tab]</c>, <c>style</c>, <c>style-new</c>, <c>dimstyle</c>,
    /// <c>dimstyle-new</c>, <c>dimstyle-modify[:Tab]</c>, <c>dimstyle-override[:Tab]</c>, <c>dimstyle-compare[:Other]</c>,
    /// <c>mleaderstyle</c>, <c>mleaderstyle-modify[:Tab]</c>, <c>matchprop</c> (of the first object selected, else the first
    /// of the space shown), <c>pastespec</c>, <c>app-menu[:new|open|saveas|export|print|close]</c> (the application menu, on Recent Documents or the
    /// list of an item), <c>cmdline-menu</c>, <c>input-settings-menu</c> and <c>recent-commands-menu</c> (the command line's
    /// right-click menu and its two submenus) and <c>autocomplete:LA</c> (the AutoComplete list for what is typed), the lines of
    /// <c>--run</c> typed first where the dialog needs them. <c>--recent "D:\a.dxf|*D:\b.dxf"</c> fills Recent Documents, the
    /// newest first, a file written with * pinned.
    /// </para>
    /// <para>
    /// A host adds its own options through the protected members: <see cref="ReadOption"/> takes them off the command line,
    /// <see cref="Attach"/> and <see cref="AttachPlugins"/> put its parts into the view model, <see cref="Prepare"/> runs
    /// before the drawing is opened, <see cref="TryCreateDialog"/> draws a dialog the presenter does not know, and
    /// <see cref="SelectAfterTab"/> selects what its options name.
    /// </para>
    /// </remarks>
    public class CadAppHost
    {
        /// <summary>Makes the host of an app.</summary>
        /// <param name="appName">
        /// The name of the app, shown before the drawing's title in the title bar and on the start screen: "GeometryHelper DXF".
        /// </param>
        /// <param name="pluginFolder">
        /// The folder of the app's command plugins: the plugins themselves in its <c>Plugins</c> folder, the startup list and the
        /// trusted folders in its <c>plugins.json</c>, as <c>%AppData%\GeometryHelper\Dxf</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the folder is null.</exception>
        public CadAppHost(string appName, string pluginFolder)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            PluginFolder = pluginFolder ?? throw new ArgumentNullException(nameof(pluginFolder));
        }

        /// <summary>Gets the name of the app, as "GeometryHelper DXF".</summary>
        public string AppName { get; }

        /// <summary>Gets the folder of the app's command plugins, their startup list and their trusted folders.</summary>
        public string PluginFolder { get; }

        /// <summary>
        /// Gets the line written to the error output when the arguments of <c>--screenshot</c> do not read: the options the
        /// presenter knows, after the name of the program.
        /// </summary>
        protected virtual string Usage => "Usage: " + (Assembly.GetEntryAssembly()?.GetName().Name ?? "app") + " --screenshot out.png [file.dxf] [--size 1600 960] [--space \"Layout1\"] [--select H1,H2] [--run \"MOVE;0,0\"] [--cursor x,y] [--type REC|{Down}] [--toggle GRID,SNAP] [--layers] [--tab Insert|Annotate|View|Plugins|Settings] [--dialog save-changes|options|settings:Category|plugins|plugin-settings|plugin-untrusted|color|truecolor|truecolor-rgb|dsettings[:Tab]|osnap-menu|snap-menu|drawings-menu|scale-menu|missing-shx|missing-shx-replace|ptype|linetype|linetype-load|select-linetype|hatch|hatch-gradient|hatch-user|hatch-solid|hatch-file|block|insert|wblock|attdef|eattedit[:Tab]|battman|battman-edit[:Tab]|battman-settings|attdia|purge|purge-nonpurgeable|classicgroup|group-order|layerstate|view|newview|units|units-direction|plot:png|plot:jpg|plot-custom|style|style-new|dimstyle|dimstyle-new|dimstyle-modify[:Tab]|dimstyle-override[:Tab]|dimstyle-compare[:Other]|mleaderstyle|mleaderstyle-modify[:Tab]|matchprop|pastespec|app-menu[:Item]|cmdline-menu|input-settings-menu|recent-commands-menu|autocomplete:Text] [--recent \"a.dxf|*b.dxf\"] [--plugin path.dll]";

        /// <summary>
        /// Runs the app on the STA thread of its entry point: the window until it is closed, or the screenshot where the first
        /// argument is <c>--screenshot</c>.
        /// </summary>
        /// <param name="args">The command-line arguments: a DXF file to open, or <c>--screenshot</c> and its options.</param>
        /// <returns>The exit code: the window's, or for a screenshot 0 when written and 2 for arguments or a file it cannot read.</returns>
        public int Run(string[] args)
        {
            args = args ?? Array.Empty<string>();

            if (args.Length > 0 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                return Screenshot(args);
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var model = new MainViewModel { Clipboard = new CadWindowsClipboard() };
            var window = new CadAppWindow(model, AppName);
            Attach(model, window.WorkspaceView);

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

            // PICKBOX, APERTURE, LWDISPLAY and POLARANG set on the command line, and the increment of Drafting Settings, too.
            model.SettingsChanged += (s, e) =>
            {
                try
                {
                    model.Settings.Save(model.SettingsPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn("Program: the settings could not be saved after a setting changed.", ex);
                }
            };

            // The command plugins: those installed and on the startup list loaded now, each result a line of the command line.
            var plugins = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(PluginFolder, "Plugins"),
                Path.Combine(PluginFolder, "plugins.json"));
            plugins.ConfirmUntrusted = path => CadQuestionWindow.ConfirmUntrustedPlugin(window, path);

            // A host's plugins of its own are found by the same manager, before the plugins of the start are loaded.
            AttachPlugins(plugins);
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

        /// <summary>
        /// Puts a host's parts into the view model before anything else is done with it: the tab, the commands and the panels
        /// of an extension. Nothing by default.
        /// </summary>
        /// <param name="model">The view model, its settings not yet applied and no drawing open.</param>
        /// <param name="workspace">
        /// The workspace of the window shown, over which the host shows its dialogs; null in screenshot mode, where no window
        /// is shown and a dialog asked for is drawn instead (see <see cref="TryCreateDialog"/>).
        /// </param>
        protected virtual void Attach(MainViewModel model, CadWorkspace workspace)
        {
        }

        /// <summary>
        /// Gives the plugin manager to a host's own plugins before any plugin is loaded, as the model drawing finds its drawing
        /// plugins through it. Nothing by default. In screenshot mode it is called only for <c>--plugin</c> or a plugin dialog.
        /// </summary>
        /// <param name="plugins">The plugin manager of the app.</param>
        protected virtual void AttachPlugins(CadPluginManager plugins)
        {
        }

        /// <summary>
        /// Reads an option of a host's own off the screenshot's command line, as <c>--new-drawing "Test bodies" Assembly</c>.
        /// False by default: the argument is then taken as the PNG's path, or the drawing's, or ignored.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="index">The index of the argument read; moved on to the last argument the option takes.</param>
        /// <returns>True when the argument is an option of the host's, read.</returns>
        protected virtual bool ReadOption(string[] args, ref int index) => false;

        /// <summary>
        /// Does what a host's options ask before the status toggles are switched and the drawing is opened, as making a model
        /// drawing. True by default.
        /// </summary>
        /// <param name="model">The view model, its plugins given.</param>
        /// <returns>False when it cannot, the reason written to the error output: the screenshot then exits with 2.</returns>
        protected virtual bool Prepare(MainViewModel model) => true;

        /// <summary>
        /// Gets the drawing <c>--dialog save-changes</c> asks about, as a model drawing made by an option of the host's; null by
        /// default, for a blank drawing named plan-level2.dxf.
        /// </summary>
        protected virtual CadDocument SaveChangesDocument => null;

        /// <summary>
        /// Makes the window of a dialog that <c>--dialog</c> names and the presenter does not know, not shown, its size taken
        /// from it (a height not set being measured from its content). By default there is none: "--dialog name: no such
        /// dialog." is written and false given.
        /// </summary>
        /// <param name="model">The view model, the drawing open and the space shown.</param>
        /// <param name="dialogName">The name given to <c>--dialog</c>.</param>
        /// <param name="window">The dialog's window.</param>
        /// <param name="root">The content of the window that is drawn.</param>
        /// <returns>False when there is no such dialog, the reason written to the error output.</returns>
        protected virtual bool TryCreateDialog(MainViewModel model, string dialogName, out Window window, out FrameworkElement root)
        {
            Console.Error.WriteLine("--dialog " + dialogName + ": no such dialog.");
            window = null;
            root = null;
            return false;
        }

        /// <summary>
        /// Selects what a host's options name once the tab of <c>--tab</c> is shown and before <c>--select</c>, as the first
        /// view of a model drawing. True and nothing changed by default.
        /// </summary>
        /// <param name="model">The view model.</param>
        /// <param name="changed">True when something was selected, so that the window is laid out again.</param>
        /// <returns>False when it cannot, the reason written to the error output: the screenshot then exits with 2.</returns>
        protected virtual bool SelectAfterTab(MainViewModel model, out bool changed)
        {
            changed = false;
            return true;
        }

        /// <summary>Draws the window with a drawing open into a PNG, with no window shown.</summary>
        private int Screenshot(string[] args)
        {
            string output = null;
            string file = null;
            string spaceName = null;
            string run = null;
            double[] cursor = null;
            string typed = null;
            bool layers = false;
            string tab = null;
            string dialogName = null;
            string pluginPath = null;
            string recent = null;
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
                else if (string.Equals(arg, "--type", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    typed = args[++i];
                }
                else if (string.Equals(arg, "--toggle", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    toggles.AddRange(args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpperInvariant()));
                }
                else if (string.Equals(arg, "--layers", StringComparison.OrdinalIgnoreCase))
                {
                    layers = true;
                }
                else if (string.Equals(arg, "--tab", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    tab = args[++i];
                }
                else if (string.Equals(arg, "--dialog", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dialogName = args[++i];
                }
                else if (string.Equals(arg, "--plugin", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    pluginPath = Path.GetFullPath(args[++i]);
                }
                else if (string.Equals(arg, "--recent", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    recent = args[++i];
                }
                else if (ReadOption(args, ref i))
                {
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
                Console.Error.WriteLine(Usage);
                return 2;
            }

            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var model = new MainViewModel();

            // A host's parts, its dialogs not hooked: a dialog asked for is drawn below, not shown.
            Attach(model, null);
            model.StubDialogs();

            // Command plugins, kept in a folder of this run under the temporary folder, every file trusted.
            if (pluginPath != null || (dialogName ?? string.Empty).StartsWith("plugin", StringComparison.OrdinalIgnoreCase))
            {
                string pluginRoot = Path.Combine(Path.GetTempPath(), "GeometryHelper", "ScreenshotPlugins", Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                var plugins = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(pluginRoot, "plugins"), Path.Combine(pluginRoot, "plugins.json"), Path.Combine(pluginRoot, "copies"))
                {
                    ConfirmUntrusted = path => true,
                };
                model.UsePlugins(plugins, loadAtStart: false);
                AttachPlugins(plugins);

                if (pluginPath != null && model.LoadPlugin(pluginPath)?.Succeeded != true)
                {
                    Console.Error.WriteLine(model.History.Last());
                    return 2;
                }
            }

            if (!Prepare(model))
            {
                return 2;
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
                // The lines of --run typed first, as MSPACE to show the VP columns of a viewport.
                foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
                {
                    model.Execute(line);
                }

                run = null;
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
                var question = CadQuestionWindow.ForSave(new[] { SaveChangesDocument ?? CadDocument.From(new DxfDatabase("AC1027"), "plan-level2.dxf") });
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
            else if (dialogName != null && model.Document != null && (dialogName.StartsWith("plot:", StringComparison.OrdinalIgnoreCase) || string.Equals(dialogName, "plot-custom", StringComparison.OrdinalIgnoreCase)))
            {
                // PLOT's dialog on a raster device, --dialog plot:png or plot:jpg, and its Custom Image Size, --dialog plot-custom.
                var job = new CadPlotJob(model.Space, CadPlotSettings.Read(model.Document, model.Space), DisplayBox.Empty, null);
                job.Settings.UseDevice(dialogName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ? CadPlotSettings.JpgDevice : CadPlotSettings.PngDevice);
                job.Settings.Area = CadPlotArea.Extents;
                var plot = new PlotDialogViewModel(model.Document, job, false, null, model.Settings.CustomPixelSizes);

                if (string.Equals(dialogName, "plot-custom", StringComparison.OrdinalIgnoreCase))
                {
                    var custom = new CustomImageSizeWindow(new CustomImageSizeViewModel(CadPaper.Pixels(2480, 1754)));
                    (window, root) = (custom, custom.Body);
                }
                else
                {
                    var dialog = new PlotWindow(plot);
                    (window, root) = (dialog, dialog.Body);
                }

                width = (int)window.Width;
                height = (int)window.Height;
            }
            else if (dialogName != null && dialogName.EndsWith("-menu", StringComparison.OrdinalIgnoreCase) && !dialogName.StartsWith("app-menu", StringComparison.OrdinalIgnoreCase))
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
                    : string.Equals(dialogName, "cmdline-menu", StringComparison.OrdinalIgnoreCase) ? model.CommandLineMenu()
                    : string.Equals(dialogName, "recent-commands-menu", StringComparison.OrdinalIgnoreCase) ? model.CommandLineMenu()[0].Items
                    : string.Equals(dialogName, "input-settings-menu", StringComparison.OrdinalIgnoreCase) ? model.CommandLineMenu()[1].Items
                    : string.Equals(dialogName, "scale-menu", StringComparison.OrdinalIgnoreCase) ? model.ScaleMenu()
                    : Array.Empty<CadMenuItemViewModel>();

                if (items.Count == 0)
                {
                    Console.Error.WriteLine("--dialog " + dialogName + ": no such menu, or nothing to show: snap-menu needs a point asked (--run \"LINE\"), drawings-menu a drawing, scale-menu a scale shown.");
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
            else if (string.Equals(dialogName, "pastespec", StringComparison.OrdinalIgnoreCase))
            {
                // Paste Special for what the lines of --run copied, or for twelve objects of plan-level2.dxf, as the design has
                // it, where they copied none; Drawing entities as a block chosen, as the design shows.
                foreach (string line in (run ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    model.Execute(line);
                }

                run = null;
                CadClipboardData data = model.Clipboard.GetData() ?? new CadClipboardData("  0\nEOF\n", null, null, "plan-level2.dxf", "Model", 12);
                var dialog = new PasteSpecialWindow(new PasteSpecialViewModel(data));
                dialog.Model.Selected = dialog.Model.Formats[1];
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
            else if (model.Document != null && BlockDialog(model, dialogName, ref run, out window, out root))
            {
                // The block dialogs, after the lines of --run.
                width = (int)window.Width;
                height = (int)window.Height;
            }
            else if (model.Document != null && ManagerDialog(model, dialogName, ref run, out window, out root))
            {
                // The managers of round six, after the lines of --run.
                width = (int)window.Width;
                height = (int)window.Height;
            }
            else if (model.Document != null && StyleDialog(model, dialogName, ref run, out window, out root))
            {
                // The Text Style dialog, the Dimension Style Manager and its dialogs, after the lines of --run.
                width = (int)window.Width;
                height = double.IsNaN(window.Height) ? MeasuredHeight(root, width) : (int)window.Height;
            }
            else if (dialogName != null && dialogName.StartsWith("app-menu", StringComparison.OrdinalIgnoreCase))
            {
                // The application menu of the G button, on Recent Documents or, after a colon, the list of an item: app-menu:export.
                FillRecent(model, recent);
                AppMenuViewModel menu = model.CreateAppMenu(AppName);
                int colon = dialogName.IndexOf(':');

                if (colon > 0)
                {
                    menu.ShownItem = menu.Items.FirstOrDefault(m => string.Equals(m.Key, dialogName.Substring(colon + 1), StringComparison.OrdinalIgnoreCase));
                }

                root = new AppMenuView { DataContext = menu };
                window = new Window { DataContext = menu };
                width = 620;
                height = BoundHeight(root, width);
            }
            else if (dialogName != null && dialogName.StartsWith("autocomplete:", StringComparison.OrdinalIgnoreCase))
            {
                // The command line's AutoComplete list for what follows the colon, the lines of --run typed first.
                foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
                {
                    model.Execute(line);
                }

                run = null;

                if (model.Document == null)
                {
                    model.NewDrawing();
                }

                model.CommandText = dialogName.Substring("autocomplete:".Length);
                model.ShowSuggestions();
                root = new CommandSuggestionList { DataContext = model };
                window = new Window { DataContext = model };
                width = 380;
                height = BoundHeight(root, width);
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
                // A dialog of the host's.
                if (!TryCreateDialog(model, dialogName, out window, out root))
                {
                    return 2;
                }

                width = (int)window.Width;
                height = double.IsNaN(window.Height) ? MeasuredHeight(root, width) : (int)window.Height;
            }
            else
            {
                window = new CadAppWindow(model, AppName);

                // The window hooks its dialogs; none may open here, so a command asking for a file or a dialog asks on the command line.
                model.StubDialogs();
                model.ApplySettings(new GlobalSettings());
                FillRecent(model, recent);
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
                    : string.Equals(tab, "Insert", StringComparison.OrdinalIgnoreCase) ? model.InsertTab
                    : string.Equals(tab, "Annotate", StringComparison.OrdinalIgnoreCase) ? model.AnnotateTab
                    : string.Equals(tab, "View", StringComparison.OrdinalIgnoreCase) ? model.ViewTab
                    : model.Extensions.FirstOrDefault(e => string.Equals(e.TabHeader, tab, StringComparison.OrdinalIgnoreCase));
                Layout(root, width, height);
            }

            if (!SelectAfterTab(model, out bool picked))
            {
                return 2;
            }

            if (picked)
            {
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

            if (typed != null && root is CadWorkspace)
            {
                // What is typed and not yet sent, the AutoComplete list shown for it; {Down} opens the options menu of the prompt.
                if (typed == "{Down}")
                {
                    model.OpenDynamicInputMenu();
                }
                else
                {
                    model.CommandText = typed;
                    model.ShowSuggestions();
                }

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

        /// <summary>
        /// The files of --recent put into Recent Documents, parted by |, the first opened last (an hour apart), a file written
        /// with * pinned in the order written; nothing for null.
        /// </summary>
        private static void FillRecent(MainViewModel model, string recent)
        {
            if (recent == null)
            {
                return;
            }

            GlobalSettings settings = model.Settings.Clone();
            string[] files = recent.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            DateTime now = DateTime.UtcNow;

            for (int i = files.Length - 1; i >= 0; i--)
            {
                settings.NoteFileOpened(Path.GetFullPath(files[i].TrimStart('*')), now.AddHours(-i));
            }

            foreach (string file in files.Where(f => f.StartsWith("*", StringComparison.Ordinal)))
            {
                settings.SetFilePinned(Path.GetFullPath(file.TrimStart('*')), true);
            }

            model.ApplySettings(settings);
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
        /// The block dialogs (C13), after the lines of --run: <c>block</c> (Block Definition, the objects selected given and the
        /// name GRID_BUBBLE), <c>insert</c> (the Blocks palette), <c>wblock</c> (Write Block), <c>attdef</c> (Attribute
        /// Definition, the attribute GRID), <c>eattedit[:Tab]</c> (the Enhanced Attribute Editor of the first INSERT with
        /// attributes, on a tab), <c>bedit[:Name]</c> (Edit Block Definition on a block, the first by default),
        /// <c>bsaveas[:Name]</c> (the same window titled Save Block As) and <c>bclose[:Name]</c> (BCLOSE's question about the
        /// block open, else the name given).
        /// </summary>
        private static bool BlockDialog(MainViewModel model, string dialogName, ref string run, out Window window, out FrameworkElement root)
        {
            window = null;
            root = null;
            string[] parts = (dialogName ?? string.Empty).Split(':');
            string kind = parts[0].ToLowerInvariant();

            if (kind != "block" && kind != "insert" && kind != "wblock" && kind != "attdef" && kind != "eattedit" && kind != "bedit" && kind != "bsaveas" && kind != "bclose")
            {
                return false;
            }

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;
            CadDocument document = model.Document;
            var selected = new List<string>(document.Selection.Handles);

            switch (kind)
            {
                case "block":
                    var block = new BlockDefinitionWindow(new BlockDefinitionViewModel(new CadBlockDefinitionRequest(document)
                    {
                        Objects = selected,
                        BasePoint = new GeometryHelper.Geometry.GeoPoint3(1200, 800, 0),
                        Description = "Grid bubble, 10 mm circle, letter as attribute GRID.",
                    }) { Name = "GRID_BUBBLE" });
                    (window, root) = (block, block.Body);
                    return true;
                case "insert":
                    var palette = new BlocksPaletteWindow(new BlocksPaletteViewModel(new CadInsertRequest(document) { Repeat = true }, model.RecentBlocks));

                    if (palette.Model.Tiles.Count > 0)
                    {
                        palette.Model.Tiles[0].IsSelected = true;
                    }

                    (window, root) = (palette, palette.Body);
                    return true;
                case "wblock":
                    var write = new WriteBlockWindow(new WriteBlockViewModel(new CadWriteBlockRequest(document) { FileName = @"D:\Plans\Workshop A\blocks\new block.dxf" }));
                    (window, root) = (write, write.Body);
                    return true;
                case "attdef":
                    var attdef = new AttributeDefinitionWindow(new AttributeDefinitionViewModel(new CadAttributeDefinitionRequest(document, null, 3.5)
                    {
                        Tag = "GRID",
                        Prompt = "Grid letter",
                        Default = "A",
                        Justification = DxfAttachmentPoint.MiddleCenter,
                    }));
                    (window, root) = (attdef, attdef.Body);
                    return true;
                case "bedit":
                case "bsaveas":
                    // Edit Block Definition on the block named after the colon, else the first of the drawing's; Save Block As empty.
                    string offered = parts.Length > 1 ? parts[1] : CadBlocks.Named(document.Database).Select(b => b.Name).FirstOrDefault() ?? CadBlockEditor.CurrentDrawing;
                    var edit = new BlockEditWindow(new BlockEditViewModel(kind == "bedit"
                        ? new CadBlockEditRequest(document, "Edit Block Definition", offered)
                        : new CadBlockEditRequest(document, "Save Block As", parts.Length > 1 ? parts[1] : string.Empty)));
                    (window, root) = (edit, edit.Body);
                    return true;
                case "bclose":
                    var close = new BlockCloseWindow(document.BlockEditor?.Name ?? (parts.Length > 1 ? parts[1] : "DOOR_900"));
                    (window, root) = (close, close.Body);
                    return true;
                default:
                    string insert = document.ModelSpace.Block.OfType<DxfBlockReference>().Where(r => r.AttributeCollection.Count > 0).Select(r => r.Handle).FirstOrDefault();
                    CadAttributeEditRequest request = insert == null ? null : CadAttributeEditRequest.Of(document, insert);

                    if (request == null)
                    {
                        Console.Error.WriteLine("--dialog eattedit: the drawing has no INSERT with attributes (--run \"-INSERT;...\").");
                        return false;
                    }

                    var editor = new AttributeEditorWindow(new AttributeEditorViewModel(request));
                    editor.ShowTab(parts.Length > 1 ? parts[1] : "Attribute");
                    (window, root) = (editor, editor.Body);
                    return true;
            }
        }

        /// <summary>
        /// The managers, after the lines of --run, each over the request its command hands the window: <c>battman</c> (the
        /// Block Attribute Manager on the first block with attributes), <c>battman-edit[:Tab]</c> (its Edit Attribute on the
        /// first row, on a tab), <c>battman-settings</c>; <c>attdia</c> (Edit Attributes of an INSERT of that block, ATTDIA
        /// set to 1); <c>purge</c> and <c>purge-nonpurgeable</c> (Purge in each mode, the current layer chosen in the second);
        /// <c>classicgroup</c> (Object Grouping, the first group chosen) and <c>group-order</c> (its Order Group); <c>layerstate</c>
        /// (the Layer States Manager, the first state chosen); <c>view</c> (the View Manager, the first named view chosen) and
        /// <c>newview</c> (its New View); <c>units</c> (Drawing Units) and <c>units-direction</c> (its Direction Control).
        /// </summary>
        private static bool ManagerDialog(MainViewModel model, string dialogName, ref string run, out Window window, out FrameworkElement root)
        {
            window = null;
            root = null;
            string[] parts = (dialogName ?? string.Empty).Split(':');
            string kind = parts[0].ToLowerInvariant();

            if (kind != "battman" && kind != "battman-edit" && kind != "battman-settings" && kind != "attdia" && kind != "purge" && kind != "purge-nonpurgeable"
                && kind != "classicgroup" && kind != "group-order" && kind != "layerstate" && kind != "view" && kind != "newview" && kind != "units" && kind != "units-direction")
            {
                return false;
            }

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;
            CadDocument document = model.Document;
            object shown = null;

            switch (kind)
            {
                case "battman":
                case "battman-edit":
                case "battman-settings":
                    model.ShowBlockAttributeManager = dialog => { shown = dialog; return true; };
                    model.Execute("BATTMAN");
                    model.ShowBlockAttributeManager = null;

                    if (!(shown is BlockAttributeManagerViewModel manager))
                    {
                        Console.Error.WriteLine("--dialog " + kind + ": the drawing has no block with attributes.");
                        return false;
                    }

                    if (kind == "battman")
                    {
                        var list = new BlockAttributeManagerWindow(manager);
                        (window, root) = (list, list.Body);
                    }
                    else if (kind == "battman-edit")
                    {
                        var edit = new EditAttributeWindow(new EditAttributeViewModel(document, manager.Block, manager.Request.Rows[0]));
                        edit.ShowTab(parts.Length > 1 ? parts[1] : "Attribute");
                        (window, root) = (edit, edit.Body);
                    }
                    else
                    {
                        var settings = new AttributeManagerSettingsWindow(new AttributeManagerSettingsViewModel(manager.Request.Settings));
                        (window, root) = (settings, settings.Body);
                    }

                    return true;
                case "attdia":
                    string block = CadBlocks.Named(document.Database).Where(b => b.OfType<DxfAttributeDefinition>().Any(d => !d.Constant)).Select(b => b.Name).FirstOrDefault();

                    if (block == null)
                    {
                        Console.Error.WriteLine("--dialog attdia: the drawing has no block with attributes.");
                        return false;
                    }

                    model.ShowEditAttributes = dialog => { shown = dialog; return true; };

                    foreach (string line in new[] { "ATTDIA", "1", "-INSERT", block, "0,0", string.Empty, string.Empty, string.Empty })
                    {
                        model.Execute(line);
                    }

                    model.ShowEditAttributes = null;
                    var values = new EditAttributesWindow((EditAttributesViewModel)shown);
                    (window, root) = (values, values.Body);
                    return true;
                case "purge":
                case "purge-nonpurgeable":
                    model.ShowPurge = dialog => { shown = dialog; return true; };
                    model.Execute("PURGE");
                    model.ShowPurge = null;
                    var purge = (PurgeViewModel)shown;

                    if (kind == "purge-nonpurgeable")
                    {
                        purge.Mode = purge.Modes[1];
                        PurgeNodeViewModel layers = purge.Nodes.FirstOrDefault(n => n.Name == "Layers");

                        if (layers != null)
                        {
                            layers.IsExpanded = true;
                            purge.Selected = layers.Children.FirstOrDefault(n => string.Equals(n.Name, document.Database.Clayer, StringComparison.OrdinalIgnoreCase)) ?? layers.Children.FirstOrDefault();
                        }
                    }

                    var dialogWindow = new PurgeWindow(purge);
                    (window, root) = (dialogWindow, dialogWindow.Body);
                    return true;
                case "layerstate":
                    model.ShowLayerStates = dialog => { shown = dialog; return true; };
                    model.Execute("LAYERSTATE");
                    model.ShowLayerStates = null;
                    var states = (LayerStatesViewModel)shown;
                    states.Selected = states.States.FirstOrDefault();
                    var statesWindow = new LayerStatesWindow(states);
                    (window, root) = (statesWindow, statesWindow.Body);
                    return true;
                case "view":
                case "newview":
                    model.ShowViewManager = dialog => { shown = dialog; return true; };
                    model.Execute("VIEW");
                    model.ShowViewManager = null;
                    var views = (ViewManagerViewModel)shown;
                    views.Selected = views.Nodes.FirstOrDefault(n => n.IsNamed) ?? views.Selected;

                    if (kind == "view")
                    {
                        var viewsWindow = new ViewManagerWindow(views);
                        (window, root) = (viewsWindow, viewsWindow.Body);
                    }
                    else
                    {
                        var newView = new NewViewWindow(new NewViewViewModel(views.Request) { ViewName = "Stair core" });
                        (window, root) = (newView, newView.Body);
                    }

                    return true;
                case "units":
                case "units-direction":
                    model.ShowDrawingUnits = dialog => { shown = dialog; return true; };
                    model.Execute("UNITS");
                    model.ShowDrawingUnits = null;
                    var units = (DrawingUnitsViewModel)shown;

                    if (kind == "units")
                    {
                        var unitsWindow = new DrawingUnitsWindow(units);
                        (window, root) = (unitsWindow, unitsWindow.Body);
                    }
                    else
                    {
                        var direction = new DirectionControlWindow(new DirectionControlViewModel(units.Request));
                        (window, root) = (direction, direction.Body);
                    }

                    return true;
                default:
                    model.ShowGroupManager = dialog => { shown = dialog; return true; };
                    model.Execute("CLASSICGROUP");
                    model.ShowGroupManager = null;
                    var groups = (GroupManagerViewModel)shown;
                    groups.Selected = groups.Groups.FirstOrDefault();

                    if (kind == "classicgroup")
                    {
                        var grouping = new GroupManagerWindow(groups);
                        (window, root) = (grouping, grouping.Body);
                    }
                    else
                    {
                        var order = new OrderGroupWindow(new OrderGroupViewModel(groups.Request));
                        (window, root) = (order, order.Body);
                    }

                    return true;
            }
        }

        /// <summary>
        /// The style dialogs (C10), after the lines of --run: <c>style</c> or <c>textstyle</c> (Text Style), <c>style-new</c> (its
        /// name asked), <c>dimstyle</c> (the Manager), <c>dimstyle-new</c> (Create New Dimension Style), <c>dimstyle-modify[:Tab]</c>
        /// (or <c>dimstyle-edit[:Tab]</c>) and
        /// <c>dimstyle-override[:Tab]</c> of the style selected or current, <c>dimstyle-compare[:Other]</c>; <c>mleaderstyle</c>
        /// (the Multileader Style Manager) and <c>mleaderstyle-modify[:Tab]</c> of the current style; <c>matchprop</c>, the
        /// Property Settings dialog of the first object selected, else the first of the space shown.
        /// </summary>
        private static bool StyleDialog(MainViewModel model, string dialogName, ref string run, out Window window, out FrameworkElement root)
        {
            window = null;
            root = null;
            string[] parts = (dialogName ?? string.Empty).Split(':');
            string kind = parts[0].ToLowerInvariant();
            kind = kind == "textstyle" ? "style" : kind == "dimstyle-edit" ? "dimstyle-modify" : kind;

            if (kind != "style" && kind != "style-new" && !kind.StartsWith("dimstyle", StringComparison.Ordinal) && kind != "mleaderstyle" && kind != "mleaderstyle-modify" && kind != "matchprop")
            {
                return false;
            }

            foreach (string line in run == null ? Array.Empty<string>() : run.Split(';'))
            {
                model.Execute(line);
            }

            run = null;

            switch (kind)
            {
                case "mleaderstyle":
                case "mleaderstyle-modify":
                    MLeaderStyleManagerViewModel styles = model.CreateMLeaderStyleManager();

                    if (kind == "mleaderstyle")
                    {
                        var list = new MLeaderStyleManagerWindow(styles);
                        (window, root) = (list, list.Body);
                        return true;
                    }

                    MLeaderStyleEditViewModel modify = styles.CreateModify();

                    if (parts.Length > 1)
                    {
                        modify.Tab = modify.Tabs.FirstOrDefault(t => string.Equals(t, parts[1], StringComparison.OrdinalIgnoreCase)) ?? modify.Tab;
                    }

                    var change = new MLeaderStyleEditWindow(modify);
                    (window, root) = (change, change.Body);
                    return true;
                case "matchprop":
                    // The source is the first object selected (--select), else the first of the space shown.
                    string source = model.Document.Selection.Handles.FirstOrDefault() ?? model.Document.CurrentSpace.Block.Cast<DxfEntity>().FirstOrDefault()?.Handle;
                    var settings = new MatchPropSettingsWindow(new MatchPropSettingsViewModel(CadMatchProperties.All, model.Document, source));
                    (window, root) = (settings, settings.Body);
                    return true;
            }

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

        /// <summary>The height a view sized to its content takes at a width, its bindings to its data context let run first.</summary>
        private static int BoundHeight(FrameworkElement root, int width)
        {
            root.Measure(new Size(width, double.PositiveInfinity));
            Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.Background);
            return MeasuredHeight(root, width);
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
