using System;
using GeometryHelper.Dxf.Viewer;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One hand edit of a drawing, as the drawing document records it: which view, dimension or mark was changed, how, and
    /// the values before and after. An update from the model makes the drawing again and applies the edits that still apply.
    /// </summary>
    /// <remarks>
    /// A mark M-3 dragged 40 to the right is <c>new DrawingEdit("M-3", CadEditKind.Move, newValue: "40,0")</c>; the text of
    /// the dimension D-7 overridden from its measured 3000 to "3000 (cut on site)" is
    /// <c>new DrawingEdit("D-7", CadEditKind.Property, "Text override", "", "3000 (cut on site)")</c>. The kinds and the form of
    /// their values are those of the presenter's <see cref="CadEdit"/>, so that an edit made there is recorded as it came.
    /// Two edits are equal when every field is the same, character by character.
    /// </remarks>
    public sealed class DrawingEdit : IEquatable<DrawingEdit>
    {
        /// <summary>Makes a record of one hand edit.</summary>
        /// <param name="objectId">The id of the view, dimension, mark or table edited, as the drawing gives its elements.</param>
        /// <param name="kind">What the edit did.</param>
        /// <param name="property">The property changed, or the grip dragged; null where the kind has none.</param>
        /// <param name="oldValue">The value before; null where the kind has none.</param>
        /// <param name="newValue">The value after; null where the kind has none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the object id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the object id is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of <see cref="CadEditKind"/>.</exception>
        public DrawingEdit(string objectId, CadEditKind kind, string property = null, string oldValue = null, string newValue = null)
        {
            if (objectId == null)
            {
                throw new ArgumentNullException(nameof(objectId));
            }

            if (objectId.Length == 0)
            {
                throw new ArgumentException("An edit names the id of what it edited.", nameof(objectId));
            }

            if (!Enum.IsDefined(typeof(CadEditKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind has to be one of CadEditKind.");
            }

            ObjectId = objectId;
            Kind = kind;
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>Gets the id of the element edited.</summary>
        public string ObjectId { get; }

        /// <summary>Gets what the edit did.</summary>
        public CadEditKind Kind { get; }

        /// <summary>Gets the property changed, or the grip dragged; null where the kind has none.</summary>
        public string Property { get; }

        /// <summary>Gets the value before the edit; null where the kind has none.</summary>
        public string OldValue { get; }

        /// <summary>Gets the value after the edit; null where the kind has none.</summary>
        public string NewValue { get; }

        /// <summary>Whether this edit is the same as another, field by field, character by character.</summary>
        /// <param name="other">The other edit.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingEdit other)
            => other != null
               && Kind == other.Kind
               && string.Equals(ObjectId, other.ObjectId, StringComparison.Ordinal)
               && string.Equals(Property, other.Property, StringComparison.Ordinal)
               && string.Equals(OldValue, other.OldValue, StringComparison.Ordinal)
               && string.Equals(NewValue, other.NewValue, StringComparison.Ordinal);

        /// <summary>Whether an object is an edit the same as this one.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DrawingEdit);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingEdit)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ObjectId);
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (Property == null ? 0 : StringComparer.Ordinal.GetHashCode(Property));
                hash = (hash * 397) ^ (OldValue == null ? 0 : StringComparer.Ordinal.GetHashCode(OldValue));
                return (hash * 397) ^ (NewValue == null ? 0 : StringComparer.Ordinal.GetHashCode(NewValue));
            }
        }

        /// <summary>Gives the edit as a line for debugging.</summary>
        /// <returns>The line, such as <c>M-3 Move: (none) -&gt; 40,0</c>.</returns>
        public override string ToString() => ObjectId + " " + Kind + (Property == null ? string.Empty : " " + Property) + ": " + (OldValue ?? "(none)") + " -> " + (NewValue ?? "(none)");
    }
}
