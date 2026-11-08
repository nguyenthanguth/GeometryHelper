using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// What a drawing document carries inside its DXF, as <see cref="DrawingDocumentStore"/> writes and reads it: the
    /// drawing's id, its views and annotations, the hand edits made to them, the plugin runs, and the records a newer
    /// version wrote that this one does not read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing B-101 of one front view with two dimensions, one moved by hand, is
    /// <c>new DrawingDocumentRecords("B-101", new[] { view }, new[] { overall, chain }, new[] { moved }, null)</c>.
    /// The lists keep the order given; the store writes them in that order and reads them back in it.
    /// </para>
    /// <para>
    /// <see cref="UnknownRecords"/> holds each record the store did not read, its groups as the DXF holds them, its opening
    /// <c>102 {NAME</c> and closing <c>102 }</c> among them: one of a kind a newer version writes, and one that failed to read.
    /// The store writes them back as they are, so that a drawing saved here loses nothing a newer version put in it.
    /// </para>
    /// <para>
    /// The records do not change once made. Two are equal when the id is the same and every list holds the same records in
    /// the same order, the unknown ones group for group, character by character.
    /// </para>
    /// </remarks>
    public sealed class DrawingDocumentRecords : IEquatable<DrawingDocumentRecords>
    {
        private readonly DrawingElement[] _views;
        private readonly DrawingElement[] _annotations;
        private readonly DrawingEdit[] _edits;
        private readonly DrawingPluginRun[] _pluginRuns;
        private readonly IReadOnlyList<DxfTypedValue>[] _unknown;

        /// <summary>Makes the records of a drawing document.</summary>
        /// <param name="drawingId">The drawing's id; null where it has none.</param>
        /// <param name="views">The views, in order; null for none.</param>
        /// <param name="annotations">The dimensions, marks, tables and other annotations, in order; null for none.</param>
        /// <param name="edits">The hand edits, in the order they were made; null for none.</param>
        /// <param name="pluginRuns">The plugin runs, in the order they ran; null for none.</param>
        /// <param name="unknownRecords">The records not read, each its groups as the DXF holds them; null for none.</param>
        /// <exception cref="ArgumentException">Thrown when a list holds a null, or an unknown record has no groups.</exception>
        public DrawingDocumentRecords(
            string drawingId,
            IEnumerable<DrawingElement> views,
            IEnumerable<DrawingElement> annotations,
            IEnumerable<DrawingEdit> edits,
            IEnumerable<DrawingPluginRun> pluginRuns,
            IEnumerable<IReadOnlyList<DxfTypedValue>> unknownRecords = null)
        {
            DrawingId = drawingId;
            _views = NoNulls(views, nameof(views));
            _annotations = NoNulls(annotations, nameof(annotations));
            _edits = NoNulls(edits, nameof(edits));
            _pluginRuns = NoNulls(pluginRuns, nameof(pluginRuns));
            var unknown = new List<IReadOnlyList<DxfTypedValue>>();

            foreach (IReadOnlyList<DxfTypedValue> record in unknownRecords ?? new IReadOnlyList<DxfTypedValue>[0])
            {
                if (record == null || record.Count == 0)
                {
                    throw new ArgumentException("An unknown record has its groups.", nameof(unknownRecords));
                }

                unknown.Add(Array.AsReadOnly(new List<DxfTypedValue>(record).ToArray()));
            }

            _unknown = unknown.ToArray();
        }

        /// <summary>Gets the drawing's id; null where it has none.</summary>
        public string DrawingId { get; }

        /// <summary>Gets the views, in order.</summary>
        public IReadOnlyList<DrawingElement> Views => _views;

        /// <summary>Gets the dimensions, marks, tables and other annotations, in order.</summary>
        public IReadOnlyList<DrawingElement> Annotations => _annotations;

        /// <summary>Gets the hand edits, in the order they were made.</summary>
        public IReadOnlyList<DrawingEdit> Edits => _edits;

        /// <summary>Gets the plugin runs, in the order they ran.</summary>
        public IReadOnlyList<DrawingPluginRun> PluginRuns => _pluginRuns;

        /// <summary>Gets the records not read, each its groups as the DXF holds them, kept to be written back.</summary>
        public IReadOnlyList<IReadOnlyList<DxfTypedValue>> UnknownRecords => _unknown;

        /// <summary>Whether these records are the same as others: the same id, and the same records in the same order.</summary>
        /// <param name="other">The other records.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingDocumentRecords other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null
                || !string.Equals(DrawingId, other.DrawingId, StringComparison.Ordinal)
                || !Same(_views, other._views)
                || !Same(_annotations, other._annotations)
                || !Same(_edits, other._edits)
                || !Same(_pluginRuns, other._pluginRuns)
                || _unknown.Length != other._unknown.Length)
            {
                return false;
            }

            for (int i = 0; i < _unknown.Length; i++)
            {
                if (!DrawingRecordGroups.SameGroups(_unknown[i], other._unknown[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether an object is records the same as these.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DrawingDocumentRecords);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingDocumentRecords)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DrawingId == null ? 0 : StringComparer.Ordinal.GetHashCode(DrawingId);
                hash = (hash * 397) ^ _views.Length;
                hash = (hash * 397) ^ _annotations.Length;
                hash = (hash * 397) ^ _edits.Length;
                hash = (hash * 397) ^ _pluginRuns.Length;
                return (hash * 397) ^ _unknown.Length;
            }
        }

        /// <summary>Gives the records as a line for debugging.</summary>
        /// <returns>The line, such as <c>B-101: 1 views, 2 annotations, 1 edits, 0 plugin runs, 0 unknown</c>.</returns>
        public override string ToString()
            => (DrawingId ?? "(no id)") + ": " + _views.Length + " views, " + _annotations.Length + " annotations, " + _edits.Length + " edits, " + _pluginRuns.Length + " plugin runs, " + _unknown.Length + " unknown";

        private static T[] NoNulls<T>(IEnumerable<T> items, string parameter)
            where T : class
        {
            var list = new List<T>();

            foreach (T item in items ?? new T[0])
            {
                list.Add(item ?? throw new ArgumentException("A list of records holds no null.", parameter));
            }

            return list.ToArray();
        }

        private static bool Same<T>(T[] a, T[] b)
            where T : class, IEquatable<T>
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
