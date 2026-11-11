using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// What an update of a drawing from the model did (<see cref="Drawing.Update(DrawingSource)"/>): the objects of the model added, taken
    /// away and changed, the views, dimensions and marks added, taken away and changed, the hand edits kept and those that no
    /// longer apply with why, and what became of the plugin runs.
    /// </summary>
    /// <remarks>
    /// A beam 3 000 long made 3 500 long changes the object of the beam, the views it is in and its overall dimension, whose
    /// text goes from 3000 to 3500; a mark moved by hand is kept as a hand edit and stands as far from its part as before; a
    /// text edited on a dimension of a part taken out of the model is dropped, "the object P-2 is no longer in the model".
    /// Every list is in the order of the ids, character by character, but the hand edits, which keep the order they were
    /// made in.
    /// </remarks>
    public sealed class UpdateReport
    {
        internal UpdateReport(
            IReadOnlyList<string> addedObjects,
            IReadOnlyList<string> removedObjects,
            IReadOnlyList<string> changedObjects,
            IReadOnlyList<string> addedElements,
            IReadOnlyList<string> removedElements,
            IReadOnlyList<string> changedElements,
            IReadOnlyList<DrawingEdit> editsKept,
            IReadOnlyList<DroppedEdit> editsDropped,
            IReadOnlyList<DrawingPluginRun> pluginRunsToRunAgain,
            IReadOnlyList<DrawingPluginRun> pluginRunsRun,
            IReadOnlyList<DroppedPluginRun> pluginRunsDropped
        )
        {
            AddedObjects = addedObjects;
            RemovedObjects = removedObjects;
            ChangedObjects = changedObjects;
            AddedElements = addedElements;
            RemovedElements = removedElements;
            ChangedElements = changedElements;
            EditsKept = editsKept;
            EditsDropped = editsDropped;
            PluginRunsToRunAgain = pluginRunsToRunAgain;
            PluginRunsRun = pluginRunsRun;
            PluginRunsDropped = pluginRunsDropped;
        }

        /// <summary>Gets the ids of the objects of the model the drawing had not, now drawn.</summary>
        public IReadOnlyList<string> AddedObjects { get; }

        /// <summary>Gets the ids of the objects of the model the drawing had, now gone from it.</summary>
        public IReadOnlyList<string> RemovedObjects { get; }

        /// <summary>
        /// Gets the ids of the objects of the model drawn before and now whose body, frame or texts changed, to the bit; empty
        /// where the drawing was opened from a file with no source, which leaves nothing to compare with.
        /// </summary>
        public IReadOnlyList<string> ChangedObjects { get; }

        /// <summary>Gets the ids of the views, dimensions and marks the drawing has now and had not.</summary>
        public IReadOnlyList<string> AddedElements { get; }

        /// <summary>Gets the ids of the views, dimensions and marks the drawing had and has no longer.</summary>
        public IReadOnlyList<string> RemovedElements { get; }

        /// <summary>Gets the ids of the views, dimensions and marks the drawing had and has, whose values or links changed.</summary>
        public IReadOnlyList<string> ChangedElements { get; }

        /// <summary>Gets the hand edits that still apply, applied again, in the order they were made.</summary>
        public IReadOnlyList<DrawingEdit> EditsKept { get; }

        /// <summary>Gets the hand edits that no longer apply, with why, in the order they were made; they are taken out of the drawing.</summary>
        public IReadOnlyList<DroppedEdit> EditsDropped { get; }

        /// <summary>
        /// Gets the plugin runs the drawing keeps but did not run, since their plugins are not in <see cref="Drawing.Plugins"/>:
        /// what they added is left out until an update with their plugins at hand runs them again. In the order they ran.
        /// </summary>
        public IReadOnlyList<DrawingPluginRun> PluginRunsToRunAgain { get; }

        /// <summary>Gets the plugin runs run again on the model as it is now, their dimensions made anew, in the order they ran.</summary>
        public IReadOnlyList<DrawingPluginRun> PluginRunsRun { get; }

        /// <summary>
        /// Gets the plugin runs that no longer apply, with why, in the order they ran: an object they were given is no longer in
        /// the model, or their view is no longer in the drawing. They are taken out of the drawing.
        /// </summary>
        public IReadOnlyList<DroppedPluginRun> PluginRunsDropped { get; }

        /// <inheritdoc/>
        public override string ToString() =>
            FormattableString.Invariant(
                $"Update: objects +{AddedObjects.Count} -{RemovedObjects.Count} ~{ChangedObjects.Count}; elements +{AddedElements.Count} -{RemovedElements.Count} ~{ChangedElements.Count}; edits kept {EditsKept.Count}, dropped {EditsDropped.Count}; plugin runs run {PluginRunsRun.Count}, to run again {PluginRunsToRunAgain.Count}, dropped {PluginRunsDropped.Count}"
            );
    }

    /// <summary>A hand edit that no longer applies after an update, and why.</summary>
    public sealed class DroppedEdit
    {
        internal DroppedEdit(DrawingEdit edit, string reason)
        {
            Edit = edit;
            Reason = reason;
        }

        /// <summary>Gets the edit.</summary>
        public DrawingEdit Edit { get; }

        /// <summary>Gets why it no longer applies, in words: "the object P-2 is no longer in the model".</summary>
        public string Reason { get; }

        /// <inheritdoc/>
        public override string ToString() => Edit + ": " + Reason;
    }

    /// <summary>A plugin run that no longer applies after an update, and why.</summary>
    public sealed class DroppedPluginRun
    {
        internal DroppedPluginRun(DrawingPluginRun run, string reason)
        {
            Run = run;
            Reason = reason;
        }

        /// <summary>Gets the run.</summary>
        public DrawingPluginRun Run { get; }

        /// <summary>Gets why it no longer applies, in words: "the object TC5-bolt-1 is no longer in the model".</summary>
        public string Reason { get; }

        /// <inheritdoc/>
        public override string ToString() => Run + ": " + Reason;
    }
}
