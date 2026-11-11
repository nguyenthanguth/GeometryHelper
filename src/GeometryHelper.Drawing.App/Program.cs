using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Drawing.Wpf;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.App
{
    /// <summary>
    /// Starts GeometryHelper Drawing: the presenter's app (<see cref="CadAppHost"/>) with the model drawing added.
    /// <c>GeometryHelper.Drawing.App.exe plan.dxf</c> opens the window on a drawing; <c>--screenshot out.png plan.dxf</c> and the
    /// options of <see cref="CadAppHost"/> draw the window or a dialog into a PNG, with no window shown, and exit: 0 when written,
    /// 2 for arguments or a file it cannot read.
    /// <c>--new-drawing "Test bodies" Assembly</c> makes a model drawing from a source with a preset and shows it, as New drawing
    /// does; <c>--tab Drawing</c> shows the Drawing ribbon tab; <c>--pick view|dimension|mark|line</c> selects the first entity of
    /// the drawing that draws one; <c>--hand-edit</c> moves its first dimension by hand; <c>--dialog new|new-source|new-settings|update|
    /// settings[:Tab]|export|message|run-plugin</c> draws that dialog of the model drawing instead of the window; <c>--plugin path.dll</c>
    /// loads a command plugin or a drawing plugin.
    /// </summary>
    public static class Program
    {
        /// <summary>The entry point.</summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            // The code pages of the big fonts (932, 936, 949, 950) and of old DXF files, which .NET (Core) reads only once registered.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return new DrawingAppHost(Path.Combine(appData, "GeometryHelper", "Drawing")).Run(args);
        }

        /// <summary>The presenter's app with the model drawing: its tab, its plugins, and its options of the screenshot.</summary>
        private sealed class DrawingAppHost : CadAppHost
        {
            private DrawingWorkspace _drawings;
            private ModelDrawing _made;
            private string _sourceName;
            private string _preset;
            private string _pick;
            private bool _handEdit;

            public DrawingAppHost(string pluginFolder)
                : base("GeometryHelper Drawing", pluginFolder) { }

            protected override string Usage =>
                "Usage: GeometryHelper.Drawing.App --screenshot out.png [file.dxf] [--size 1600 960] [--space \"Layout1\"] [--select H1,H2] [--run \"MOVE;0,0\"] [--cursor x,y] [--toggle GRID,SNAP] [--layers] [--new-drawing \"Test bodies\" Assembly] [--tab Drawing] [--pick view|dimension|mark|line] [--hand-edit] [--dialog new|new-source|new-settings|update|settings[:Tab]|export|message|run-plugin|plugins|plugin-settings|plugin-untrusted|color|truecolor|dsettings[:Tab]|osnap-menu|snap-menu|drawings-menu|missing-shx|missing-shx-replace|ptype|block|insert|wblock|attdef|eattedit[:Tab]|pastespec] [--plugin path.dll]";

            protected override CadDocument SaveChangesDocument => _made?.Document;

            protected override void Attach(MainViewModel model, CadWorkspace workspace)
            {
                if (workspace != null)
                {
                    _drawings = workspace.AddModelDrawings(
                        model,
                        DrawingWorkspaceExtensions.DefaultSources(Tolerance.Default),
                        Tolerance.Default
                    );
                    return;
                }

                // The model drawing, its dialogs not hooked: a dialog asked for is drawn, not shown.
                _drawings = new DrawingWorkspace(
                    model,
                    DrawingWorkspaceExtensions.DefaultSources(Tolerance.Default),
                    Tolerance.Default
                );
                _drawings.TabContent = new DrawingRibbonTab { DataContext = _drawings };
            }

            // The drawing plugins are loaded by the same manager as the command plugins.
            protected override void AttachPlugins(CadPluginManager plugins) =>
                _drawings.Plugins = new DrawingPluginCatalog(plugins);

            protected override bool ReadOption(string[] args, ref int index)
            {
                string arg = args[index];

                if (string.Equals(arg, "--new-drawing", StringComparison.OrdinalIgnoreCase) && index + 2 < args.Length)
                {
                    _sourceName = args[++index];
                    _preset = args[++index];
                }
                else if (string.Equals(arg, "--pick", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    _pick = args[++index];
                }
                else if (string.Equals(arg, "--hand-edit", StringComparison.OrdinalIgnoreCase))
                {
                    _handEdit = true;
                }
                else
                {
                    return false;
                }

                return true;
            }

            protected override bool Prepare(MainViewModel model)
            {
                if (_sourceName == null)
                {
                    return true;
                }

                _made = MakeDrawing(_drawings, _sourceName, _preset);

                if (_made == null)
                {
                    return false;
                }

                if (_handEdit && !HandEdit(_made))
                {
                    Console.Error.WriteLine("The drawing has no dimension to move.");
                    return false;
                }

                return true;
            }

            protected override bool TryCreateDialog(
                MainViewModel model,
                string dialogName,
                out Window window,
                out FrameworkElement root
            )
            {
                DialogViewModel dialog = Dialog(_drawings, dialogName);

                if (dialog == null)
                {
                    Console.Error.WriteLine(
                        "--dialog " + dialogName + ": no such dialog, or it needs --new-drawing first."
                    );
                    window = null;
                    root = null;
                    return false;
                }

                DrawingDialogWindow dialogWindow = DrawingWorkspaceExtensions.CreateDialog(dialog);
                window = dialogWindow;
                root = dialogWindow.Body;
                return true;
            }

            protected override bool SelectAfterTab(MainViewModel model, out bool changed)
            {
                changed = false;

                if (_pick == null || _made == null)
                {
                    return true;
                }

                string handle = Pick(_made, _pick);

                if (handle == null)
                {
                    Console.Error.WriteLine("--pick " + _pick + ": the drawing has no such entity in the space shown.");
                    return false;
                }

                _made.Document.Selection.Set(new[] { handle });
                changed = true;
                return true;
            }
        }

        /// <summary>Makes a model drawing through the New drawing dialog's steps: the source named, the preset, Create.</summary>
        private static ModelDrawing MakeDrawing(DrawingWorkspace drawings, string sourceName, string preset)
        {
            NewDrawingViewModel dialog = drawings.ShowNewDrawing();
            SourceOption source = dialog.Sources.FirstOrDefault(s =>
                string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase)
            );

            if (source == null || !source.IsAvailable)
            {
                Console.Error.WriteLine(
                    "--new-drawing: no source named "
                        + sourceName
                        + " can give objects; there are "
                        + string.Join(", ", dialog.Sources.Select(s => s.Name))
                        + "."
                );
                return null;
            }

            source.IsSelected = true;
            Pump(dialog.NextAsync());

            if (!NewDrawingViewModel.PresetNames.Contains(preset, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("--new-drawing: the preset is Part, Assembly or General.");
                return null;
            }

            dialog.Preset = NewDrawingViewModel.PresetNames.First(p =>
                string.Equals(p, preset, StringComparison.OrdinalIgnoreCase)
            );
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
            return handle != null
                && made.Document.Owners.TryApply(
                    made.Document,
                    new CadEdit(handle, CadEditKind.Move, null, null, "0,25")
                );
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
                        update.Source = drawings.Providers.FirstOrDefault(p =>
                            p.IsAvailable && p.Name.EndsWith("changed", StringComparison.Ordinal)
                        ) ?? update.Source;
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
                    return MessageViewModel.Failure(
                        "Drawing not made",
                        "B-105 could not be made from 4 parts.",
                        new ArgumentException("The settings allow no scale.")
                    );
                case "run-plugin":
                    return drawings.ShowRunPlugin();
                default:
                    return null;
            }
        }

        /// <summary>The first entity of a drawing, in the space shown, that draws a view, a dimension, a mark or a line of the model.</summary>
        private static string Pick(ModelDrawing made, string kind, bool anySpace = false)
        {
            var kinds = made
                .Drawing.Elements.GroupBy(e => e.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Kind, StringComparer.Ordinal);

            foreach (DxfEntity entity in DrawingEntityTags.Find(made.Document.Database))
            {
                if (
                    entity.IsErased
                    || !DrawingEntityTags.TryRead(entity, out string id, out _)
                    || (!anySpace && made.Document.FindSpace(entity.Handle) != made.Document.CurrentSpace)
                )
                {
                    continue;
                }

                bool line = !kinds.ContainsKey(id) && entity is DxfLine;

                if (
                    string.Equals(kind, "line", StringComparison.OrdinalIgnoreCase)
                        ? line
                        : kinds.TryGetValue(id, out string found)
                            && string.Equals(found, kind, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return entity.Handle;
                }
            }

            return null;
        }

        /// <summary>Waits for a task while the dispatcher runs, so that its awaits come back to this thread.</summary>
        private static T Pump<T>(Task<T> task)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            return task.GetAwaiter().GetResult();
        }
    }
}
