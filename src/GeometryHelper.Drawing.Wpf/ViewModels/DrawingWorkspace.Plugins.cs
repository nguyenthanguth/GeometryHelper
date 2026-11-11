using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf.Viewer;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The drawing plugins run on the drawing shown: Run plugin on the Drawing tab, which shows its dialog, and PLUGIN on the
    /// command line, each run one undo step of the drawing.
    /// </summary>
    public sealed partial class DrawingWorkspace
    {
        private const string NoPluginText =
            "No drawing plugin is loaded: load one with PLUGINLOAD, or in the Plugin manager.";

        /// <summary>Asks the window for the Run plugin dialog of the drawing shown; null, said on the command line, with no model drawing shown or no drawing plugin loaded.</summary>
        /// <returns>The dialog's view model, or null.</returns>
        public PluginRunViewModel ShowRunPlugin()
        {
            if (_current == null)
            {
                Say("Run plugin works on a model drawing; the drawing shown is not one.");
                return null;
            }

            if (LoadedPlugins.Count == 0)
            {
                Say(NoPluginText);
                return null;
            }

            return Ask(DrawingDialogKind.RunPlugin, new PluginRunViewModel(this, _current));
        }

        /// <summary>
        /// Runs a drawing plugin in a view of the drawing shown, as <see cref="Drawing.RunPlugin"/> does, on the window's thread:
        /// one undo step, which takes the run back with its dimensions. What came of it, or why it was not run, is said on the
        /// command line, and nothing is thrown: a plugin that fails leaves the drawing as it was.
        /// </summary>
        /// <param name="plugin">The plugin.</param>
        /// <param name="viewId">The id of the view, V-FRONT.</param>
        /// <param name="objectIds">The ids of the objects given to it; null or none for every object of the view of the kinds it asks for.</param>
        /// <param name="settings">Values of its settings by name; null or one left out for its default.</param>
        /// <returns>What changed; null where it was not run.</returns>
        /// <remarks>
        /// The sample Bolt dimensions run in the front view of the column C1 adds the hole pitches of its base plate, and the
        /// command line says <c>Bolt dimensions in V-FRONT: 5 elements added, 0 changed, 0 taken away; 0 hand edits kept.</c>
        /// While the drawing is being made again it says <c>C1 is being made again; wait for it.</c> and runs nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the plugin or the view's id is null.</exception>
        public UpdateReport RunPlugin(
            DrawingPlugin plugin,
            string viewId,
            IEnumerable<string> objectIds = null,
            IEnumerable<KeyValuePair<string, double>> settings = null
        ) => RunPlugin(plugin, viewId, objectIds, settings, out _);

        /// <summary>Runs a drawing plugin as <see cref="RunPlugin(DrawingPlugin, string, IEnumerable{string}, IEnumerable{KeyValuePair{string, double}})"/> does, giving the words of why it was not run.</summary>
        internal UpdateReport RunPlugin(
            DrawingPlugin plugin,
            string viewId,
            IEnumerable<string> objectIds,
            IEnumerable<KeyValuePair<string, double>> settings,
            out string failure
        )
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            if (viewId == null)
            {
                throw new ArgumentNullException(nameof(viewId));
            }

            ModelDrawing entry = _current;
            string error =
                entry == null ? "No model drawing is shown."
                : _busy.Contains(entry) ? BusyText(entry)
                : null;
            string what = plugin.Name + " in " + viewId;
            UpdateReport report = Run(
                what,
                (e, provider) =>
                {
                    try
                    {
                        return e.Drawing.RunPlugin(plugin, viewId, objectIds, settings);
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException))
                    {
                        error = what + " not made: " + ex.Message.Split('\n')[0].Trim();
                        throw;
                    }
                },
                entry?.PresetName ?? "Changed"
            );

            failure = report == null ? error ?? what + " not made." : null;
            return report;
        }

        /// <summary>The drawing plugins at hand, in the catalog's order; none without a catalog.</summary>
        internal IReadOnlyList<DrawingPlugin> LoadedPlugins =>
            _plugins?.Plugins ?? (IReadOnlyList<DrawingPlugin>)Array.Empty<DrawingPlugin>();

        /// <summary>What a plugin needs, as its <see cref="DrawingPlugin.DefineInput"/> says; null, with the words of why, where it throws.</summary>
        internal static DrawingPluginInput InputOf(DrawingPlugin plugin, out string failure)
        {
            var input = new DrawingPluginInput();
            failure = null;

            try
            {
                plugin.DefineInput(input);
                return input;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                GeometryHelperLog.Warn(
                    "DrawingWorkspace: the plugin " + plugin.Name + " failed to say what it needs.",
                    ex
                );
                failure =
                    "The plugin " + plugin.Name + " failed to say what it needs: " + ex.Message.Split('\n')[0].Trim();
                return null;
            }
        }

        /// <summary>A number written to the bit, as a setting's default and range are shown: 0.01, 100.</summary>
        internal static string Exact(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A setting's range in words: ", from 0.01 to 100", ", at least 0", or nothing where it has none.</summary>
        internal static string RangeOf(DrawingPluginSetting setting)
        {
            bool low = !double.IsInfinity(setting.Minimum),
                high = !double.IsInfinity(setting.Maximum);
            return low && high ? ", from " + Exact(setting.Minimum) + " to " + Exact(setting.Maximum)
                : low ? ", at least " + Exact(setting.Minimum)
                : high ? ", at most " + Exact(setting.Maximum)
                : string.Empty;
        }

        /// <summary>
        /// PLUGIN: the plugin by name, the view, then each setting the plugin declares, Enter taking its default; the plugin is
        /// given every object of the view of the kinds it asks for, and run once the command has ended.
        /// </summary>
        private IEnumerable<CadPrompt> PluginPrompts(CadCommandContext c)
        {
            if (Find(c.Document) == null)
            {
                c.Write("PLUGIN works on a model drawing; " + c.Document.Title + " is a DXF.");
                yield break;
            }

            IReadOnlyList<DrawingPlugin> plugins = LoadedPlugins;

            if (plugins.Count == 0)
            {
                c.Write(NoPluginText);
                yield break;
            }

            if (MakeFirst(c))
            {
                yield break;
            }

            List<string> names = plugins.Select(p => p.Name).ToList();
            yield return CadPrompt.ForKeyword("Plugin name", names, names[0]);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            DrawingPlugin plugin = plugins.First(p => string.Equals(p.Name, c.Input.Text, StringComparison.Ordinal));
            DrawingPluginInput input = InputOf(plugin, out string failure);

            if (input == null)
            {
                c.Write(failure);
                yield break;
            }

            List<string> views = _current.Drawing.Views.Select(v => v.Id).ToList();

            if (views.Count == 0)
            {
                c.Write(_current.Number + " has no views to run a plugin in.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("View", views, views[0]);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string viewId = c.Input.Text;
            var values = new List<KeyValuePair<string, double>>();

            foreach (DrawingPluginSetting setting in input.Settings)
            {
                double value = setting.Default;

                while (true)
                {
                    yield return CadPrompt.ForDistance(
                        setting.Name + RangeOf(setting),
                        null,
                        null,
                        Exact(setting.Default)
                    );

                    if (c.Input.Kind == CadInputKind.None)
                    {
                        break;
                    }

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        yield break;
                    }

                    if (c.Input.Number >= setting.Minimum && c.Input.Number <= setting.Maximum)
                    {
                        value = c.Input.Number;
                        break;
                    }

                    string range = RangeOf(setting);
                    c.Write(
                        setting.Name
                            + (range.Length == 0 ? " has to be a number." : " has to be" + range.Substring(1) + ".")
                    );
                }

                values.Add(new KeyValuePair<string, double>(setting.Name, value));
            }

            c.Write(plugin.Name + " in " + viewId + ": the drawing is made again.");
            Later(() => RunPlugin(plugin, viewId, null, values));
        }
    }
}
