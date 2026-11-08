using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One run of a drawing plugin, as the drawing document keeps it so that an update runs the plugin again on the model
    /// as it is then: the plugin's name and version, the view it ran in, the objects and points it was given, and its
    /// settings by name.
    /// </summary>
    /// <remarks>
    /// A plugin BoltDimensions 1.2 run in the front view V-1 on the bolt group G-4 with a gap of 8 is
    /// <c>new DrawingPluginRun("R-1", "BoltDimensions", "1.2", "V-1", new DrawingLinks(new[] { "G-4" }), new[] { new KeyValuePair&lt;string, string&gt;("Gap", "8") })</c>.
    /// A run does not change once made; two are equal when every field is the same, the settings in the same order,
    /// character by character, and the inputs as <see cref="DrawingLinks"/> compares them.
    /// </remarks>
    public sealed class DrawingPluginRun : IEquatable<DrawingPluginRun>
    {
        private readonly KeyValuePair<string, string>[] _settings;

        /// <summary>Makes the record of a plugin run.</summary>
        /// <param name="id">The run's id, unique in its drawing.</param>
        /// <param name="plugin">The plugin's name.</param>
        /// <param name="version">The plugin's version; null where it gave none.</param>
        /// <param name="viewId">The id of the view it ran in; null for a run on the whole drawing.</param>
        /// <param name="inputs">The objects and points it was given; null for none.</param>
        /// <param name="settings">Its settings by name, in the order given; null for none. A value may be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id or the plugin's name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id or the plugin's name is empty, or a setting has a null name.</exception>
        public DrawingPluginRun(string id, string plugin, string version = null, string viewId = null, DrawingLinks inputs = null, IEnumerable<KeyValuePair<string, string>> settings = null)
        {
            Id = DrawingRecordGroups.Named(id, nameof(id), "A plugin run needs an id.");
            Plugin = DrawingRecordGroups.Named(plugin, nameof(plugin), "A plugin run names its plugin.");
            Version = version;
            ViewId = viewId;
            Inputs = inputs ?? DrawingLinks.Empty;
            _settings = DrawingRecordGroups.Values(settings, nameof(settings));
        }

        /// <summary>Gets the run's id.</summary>
        public string Id { get; }

        /// <summary>Gets the plugin's name.</summary>
        public string Plugin { get; }

        /// <summary>Gets the plugin's version; null where it gave none.</summary>
        public string Version { get; }

        /// <summary>Gets the id of the view it ran in; null for a run on the whole drawing.</summary>
        public string ViewId { get; }

        /// <summary>Gets the objects and points it was given.</summary>
        public DrawingLinks Inputs { get; }

        /// <summary>Gets its settings by name, in the order given.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Settings => _settings;

        /// <summary>Whether this run is the same as another, field by field.</summary>
        /// <param name="other">The other run.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingPluginRun other)
            => other != null
               && string.Equals(Id, other.Id, StringComparison.Ordinal)
               && string.Equals(Plugin, other.Plugin, StringComparison.Ordinal)
               && string.Equals(Version, other.Version, StringComparison.Ordinal)
               && string.Equals(ViewId, other.ViewId, StringComparison.Ordinal)
               && Inputs.Equals(other.Inputs)
               && DrawingRecordGroups.SameValues(_settings, other._settings);

        /// <summary>Whether an object is a run the same as this one.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DrawingPluginRun);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingPluginRun)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Id);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Plugin);
                return (hash * 397) ^ Inputs.GetHashCode();
            }
        }

        /// <summary>Gives the run as a line for debugging.</summary>
        /// <returns>The line, such as <c>BoltDimensions 1.2 R-1 in V-1</c>.</returns>
        public override string ToString() => Plugin + (Version == null ? string.Empty : " " + Version) + " " + Id + (ViewId == null ? string.Empty : " in " + ViewId);
    }
}
