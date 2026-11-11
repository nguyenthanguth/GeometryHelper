using System.Collections.Generic;
using GeometryHelper.Drawing.Plugins;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A drawing made again but not yet written: its views, annotation and sheet worked out from a source and settings by
    /// <see cref="Drawing.Prepare"/>, to be written into the drawing's DXF by <see cref="Drawing.Apply"/>.
    /// </summary>
    /// <remarks>
    /// Making is the slow part of an update and touches neither the drawing nor its DXF, so a window can prepare on a thread
    /// of its own and apply on its own thread: the 1 000 members of a general arrangement are prepared in about a second
    /// and applied in a tenth of that. An update is applied once, to the drawing that prepared it, and only while that drawing
    /// has not been made again since; it does not change.
    /// </remarks>
    public sealed class PreparedUpdate
    {
        internal PreparedUpdate(
            Drawing drawing,
            DrawingSource source,
            DrawingSettings settings,
            DrawingContent content,
            List<DrawingElement> views,
            List<DrawingElement> annotations,
            DrawingPluginRuns runs
        )
        {
            Drawing = drawing;
            Source = source;
            SettingsInUse = settings;
            Content = content;
            Views = views;
            Annotations = annotations;
            Runs = runs;
        }

        /// <summary>Gets the drawing the update was prepared for.</summary>
        public Drawing Drawing { get; }

        /// <summary>Gets the objects the drawing is made from.</summary>
        public DrawingSource Source { get; }

        /// <summary>Gets a copy of the settings the drawing is made with; changing the copy changes nothing of the update.</summary>
        public DrawingSettings Settings => SettingsInUse.Clone();

        /// <summary>Gets the scale the views come out at: 20 for 1:20.</summary>
        public double Scale => Content.Scale;

        /// <summary>Gets what went wrong in making the drawing, in words: views that did not fit the sheet.</summary>
        public IReadOnlyList<string> Warnings => Content.Warnings.AsReadOnly();

        /// <summary>Gets whether the update has been applied; an update is applied once.</summary>
        public bool IsApplied { get; internal set; }

        internal DrawingSettings SettingsInUse { get; }

        internal DrawingContent Content { get; }

        internal List<DrawingElement> Views { get; }

        internal List<DrawingElement> Annotations { get; }

        /// <summary>What became of the drawing's plugin runs, run again on the views made.</summary>
        internal DrawingPluginRuns Runs { get; }

        /// <summary>How many times the drawing had been made again when the update was prepared; applied only while it still is.</summary>
        internal int Generation { get; set; }
    }
}
