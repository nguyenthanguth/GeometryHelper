using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// One edit of one entity, as the presenter hands it to the entity's owner (<see cref="ICadEntityOwner.TryApply"/>)
    /// before it changes anything: which entity, what kind of edit, which property, and the values before and after.
    /// </summary>
    /// <remarks>
    /// A mark of handle 2F moved 10 to the right is <c>new CadEdit("2F", CadEditKind.Move, newValue: "10,0")</c>; its layer
    /// changed from Mark to Notes in Properties is <c>new CadEdit("2F", CadEditKind.Property, "Layer", "Mark", "Notes")</c>.
    /// <see cref="CadEditKind"/> says what each kind's values hold. An edit does not change once made.
    /// </remarks>
    public sealed class CadEdit
    {
        /// <summary>Makes an edit of one entity.</summary>
        /// <param name="handle">The handle of the entity edited, such as "2F".</param>
        /// <param name="kind">What the edit does.</param>
        /// <param name="property">The property changed, or the grip dragged; null where the kind has none.</param>
        /// <param name="oldValue">The value before the edit; null where the kind has none.</param>
        /// <param name="newValue">The value after the edit; null where the kind has none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the handle is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of <see cref="CadEditKind"/>.</exception>
        public CadEdit(string handle, CadEditKind kind, string property = null, string oldValue = null, string newValue = null)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Length == 0)
            {
                throw new ArgumentException("An edit names the handle of its entity.", nameof(handle));
            }

            if (!Enum.IsDefined(typeof(CadEditKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind has to be one of CadEditKind.");
            }

            Handle = handle;
            Kind = kind;
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>Gets the handle of the entity edited.</summary>
        public string Handle { get; }

        /// <summary>Gets what the edit does.</summary>
        public CadEditKind Kind { get; }

        /// <summary>Gets the property changed, or the index of the grip dragged; null where the kind has none.</summary>
        public string Property { get; }

        /// <summary>Gets the value before the edit; null where the kind has none.</summary>
        public string OldValue { get; }

        /// <summary>Gets the value after the edit; null where the kind has none.</summary>
        public string NewValue { get; }

        /// <summary>Gives the edit as a line for debugging.</summary>
        /// <returns>The line, such as <c>2F Property Layer: Mark -&gt; Notes</c>.</returns>
        public override string ToString() => Handle + " " + Kind + (Property == null ? string.Empty : " " + Property) + ": " + (OldValue ?? "(none)") + " -> " + (NewValue ?? "(none)");
    }
}
