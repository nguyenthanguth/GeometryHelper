using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Plugins
{
    /// <summary>What became of a drawing's plugin runs as it was made again.</summary>
    internal sealed class DrawingPluginRuns
    {
        /// <summary>The runs the drawing keeps: those run, those that failed and those whose plugin is not at hand, in their order.</summary>
        public List<DrawingPluginRun> Kept { get; } = new List<DrawingPluginRun>();

        public List<DrawingPluginRun> Ran { get; } = new List<DrawingPluginRun>();

        public List<DrawingPluginRun> Waiting { get; } = new List<DrawingPluginRun>();

        public List<DroppedPluginRun> Dropped { get; } = new List<DroppedPluginRun>();

        /// <summary>What the run added first threw; null where it ran.</summary>
        public Exception Failure { get; set; }
    }

    /// <summary>Runs a drawing's plugin runs again on the views of a drawing made again, their dimensions added to the views.</summary>
    internal static class DrawingPluginRunner
    {
        /// <summary>
        /// Runs each run with its plugin, the one given for the run added and the catalog's by name for the others: a run whose
        /// view or objects have gone is dropped; one whose plugin is not at hand is kept to be run again; one whose plugin
        /// throws is kept, nothing it added is used, and the drawing's warnings say so.
        /// </summary>
        public static DrawingPluginRuns Run(
            IReadOnlyList<DrawingPluginRun> runs,
            DrawingPluginRun added,
            DrawingPlugin addedPlugin,
            DrawingPluginCatalog catalog,
            DrawingContent content,
            DrawingSource source,
            DrawingSettings settings,
            Tolerance tolerance
        )
        {
            var result = new DrawingPluginRuns();
            var all = new List<DrawingPluginRun>(runs);

            if (added != null)
            {
                all.Add(added);
            }

            foreach (DrawingPluginRun run in all)
            {
                DrawingPlugin plugin = ReferenceEquals(run, added) ? addedPlugin : catalog?.Find(run.Plugin);

                if (plugin == null)
                {
                    result.Waiting.Add(run);
                    result.Kept.Add(run);
                    continue;
                }

                DrawingView view = content.Views.Find(v => string.Equals(v.Id, run.ViewId, StringComparison.Ordinal));
                string reason =
                    view == null
                        ? "the drawing no longer has the view " + (run.ViewId ?? "(none)")
                        : Missing(run, source);

                if (reason != null)
                {
                    result.Dropped.Add(new DroppedPluginRun(run, reason));
                    continue;
                }

                try
                {
                    var input = new DrawingPluginInput();
                    plugin.DefineInput(input);
                    var context = new DrawingPluginContext(
                        run,
                        view,
                        Objects(run, view, input, source),
                        source,
                        settings,
                        Values(run, input),
                        tolerance
                    );
                    plugin.Run(context);
                    Add(view, run, context.Dimensions);
                    result.Ran.Add(run);
                }
                catch (Exception exception)
                {
                    string line =
                        "The plugin "
                        + run.Plugin
                        + " failed in its run "
                        + run.Id
                        + ": "
                        + exception.Message
                        + " What it added is left out.";
                    GeometryHelperLog.Warn("DrawingPluginRunner: " + line, exception);
                    content.Warnings.Add(line);

                    if (ReferenceEquals(run, added))
                    {
                        result.Failure = exception;
                    }
                }

                result.Kept.Add(run);
            }

            return result;
        }

        /// <summary>Why a run's objects are no longer there; null where they all are.</summary>
        internal static string Missing(DrawingPluginRun run, DrawingSource source)
        {
            var missing = new List<string>();

            foreach (string id in run.Inputs.ObjectIds)
            {
                if (!source.TryGetObject(id, out _) && !missing.Contains(id))
                {
                    missing.Add(id);
                }
            }

            return missing.Count == 0 ? null
                : missing.Count == 1 ? "the object " + missing[0] + " is no longer in the model"
                : "the objects " + string.Join(", ", missing) + " are no longer in the model";
        }

        /// <summary>The objects a run was given; every object of its view of the kinds asked for where it was given none.</summary>
        private static List<DrawingObject> Objects(
            DrawingPluginRun run,
            DrawingView view,
            DrawingPluginInput input,
            DrawingSource source
        )
        {
            var objects = new List<DrawingObject>();
            bool given = run.Inputs.ObjectIds.Count > 0;

            foreach (string id in given ? run.Inputs.ObjectIds : view.ObjectIds)
            {
                if (
                    source.TryGetObject(id, out DrawingObject item)
                    && !objects.Contains(item)
                    && (given || input.ObjectKinds.Count == 0 || Contains(input.ObjectKinds, item.Kind))
                )
                {
                    objects.Add(item);
                }
            }

            return objects;
        }

        private static bool Contains(IReadOnlyList<DrawingObjectKind> kinds, DrawingObjectKind kind)
        {
            foreach (DrawingObjectKind k in kinds)
            {
                if (k == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The settings of a run by name: the plugin's defaults, then the run's values that read as numbers.</summary>
        private static Dictionary<string, double> Values(DrawingPluginRun run, DrawingPluginInput input)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (DrawingPluginSetting setting in input.Settings)
            {
                values[setting.Name] = setting.Default;
            }

            foreach (KeyValuePair<string, string> value in run.Settings)
            {
                if (
                    values.ContainsKey(value.Key)
                    && double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                )
                {
                    values[value.Key] = number;
                }
            }

            return values;
        }

        /// <summary>
        /// Adds a run's dimensions to its view, each known by the view's id, the run's, /D-, its kind, its axis and its number
        /// among those alike, as V-FRONT/R-1/D-HolesByGroup-X-1, so that it does not take the id of one of the drawing's own.
        /// </summary>
        private static void Add(DrawingView view, DrawingPluginRun run, List<DrawingDimension> added)
        {
            if (added.Count == 0)
            {
                return;
            }

            var dimensions = new List<DrawingDimension>(view.Dimensions);
            var ids = new List<string>(view.DimensionIds);
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (DrawingDimension d in added)
            {
                string key = d.Kind + "-" + (d.Direction.X > 0.5 ? "X" : "Y");
                counts.TryGetValue(key, out int count);
                counts[key] = ++count;
                string local = run.Id + "/D-" + key + "-" + count.ToString(CultureInfo.InvariantCulture);
                var points = new DimensionPoint[d.Points.Count];
                var lines = new GeoLine2[d.ExtensionLines.Count];

                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = d.Points[i];
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = d.ExtensionLines[i];
                }

                // The writer finds a dimension of a view by its own id, so the plugin's are given ids of their own.
                dimensions.Add(
                    new DrawingDimension(
                        local,
                        d.Kind,
                        points,
                        d.Value,
                        d.Count,
                        d.Text,
                        d.Direction,
                        d.Tier,
                        d.Offset,
                        d.DimensionLine,
                        lines,
                        d.TextBox,
                        d.TextRotation,
                        d.TextHeight,
                        d.Scale,
                        d.Rounding
                    )
                );
                ids.Add(view.Id + "/" + local);
            }

            view.Dimensions = dimensions;
            view.DimensionIds = ids;
        }
    }
}
