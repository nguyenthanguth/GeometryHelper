using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One view, dimension, mark or table of a drawing, as the drawing document keeps it: its id, what it is, the view it is
    /// in, what it is tied to in the model, and its own values by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The id is what the drawing writes into the tag of each entity it draws for the element, so that an edit of one of
    /// them finds its element, and the next update recognises it. The kind and the names of the values are the drawing's
    /// own: the front view at 1:20 may be <c>new DrawingElement("V-1", "View", null, links, new[] { new KeyValuePair&lt;string, string&gt;("Scale", "20") })</c>,
    /// and the overall dimension in it <c>new DrawingElement("D-7", "Dimension", "V-1", dimensionLinks)</c>.
    /// </para>
    /// <para>
    /// An element does not change once made. Two are equal when every field is the same, the values in the same order,
    /// character by character, and the links as <see cref="DrawingLinks"/> compares them.
    /// </para>
    /// </remarks>
    public sealed class DrawingElement : IEquatable<DrawingElement>
    {
        private readonly KeyValuePair<string, string>[] _values;

        /// <summary>Makes an element of the drawing document.</summary>
        /// <param name="id">The element's id, unique in its drawing and the same from one update to the next.</param>
        /// <param name="kind">What the element is, in the drawing's words: "View", "Dimension", "Mark".</param>
        /// <param name="viewId">The id of the view the element is in; null for a view, or for an element of the sheet.</param>
        /// <param name="links">What it is tied to in the model; null for nothing.</param>
        /// <param name="values">Its own values by name, in the order given; null for none. A value may be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id or the kind is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id or the kind is empty, or a value has a null name.</exception>
        public DrawingElement(
            string id,
            string kind,
            string viewId = null,
            DrawingLinks links = null,
            IEnumerable<KeyValuePair<string, string>> values = null
        )
        {
            Id = DrawingRecordGroups.Named(id, nameof(id), "An element needs an id.");
            Kind = DrawingRecordGroups.Named(kind, nameof(kind), "An element needs a kind.");
            ViewId = viewId;
            Links = links ?? DrawingLinks.Empty;
            _values = DrawingRecordGroups.Values(values, nameof(values));
        }

        /// <summary>Gets the element's id.</summary>
        public string Id { get; }

        /// <summary>Gets what the element is.</summary>
        public string Kind { get; }

        /// <summary>Gets the id of the view the element is in; null where it is in none.</summary>
        public string ViewId { get; }

        /// <summary>Gets what the element is tied to in the model.</summary>
        public DrawingLinks Links { get; }

        /// <summary>Gets the element's own values by name, in the order given.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Values => _values;

        /// <summary>Whether this element is the same as another, field by field.</summary>
        /// <param name="other">The other element.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingElement other) =>
            other != null
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
            && string.Equals(ViewId, other.ViewId, StringComparison.Ordinal)
            && Links.Equals(other.Links)
            && DrawingRecordGroups.SameValues(_values, other._values);

        /// <summary>Whether an object is an element the same as this one.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DrawingElement);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingElement)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Id);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Kind);
                return (hash * 397) ^ Links.GetHashCode();
            }
        }

        /// <summary>Gives the element as a line for debugging.</summary>
        /// <returns>The line, such as <c>Dimension D-7 in V-1</c>.</returns>
        public override string ToString() => Kind + " " + Id + (ViewId == null ? string.Empty : " in " + ViewId);
    }
}
