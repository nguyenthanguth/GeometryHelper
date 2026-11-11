using System;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// One field of a mark's text: the position mark, the profile, the length, the count of bolts, an attribute.
    /// </summary>
    /// <remarks>
    /// The fields are the shared instances below, and <see cref="Attribute(string)"/> for a named attribute: a part mark
    /// showing the mark and the attribute "WEIGHT" is <c>new MarkFormat(new[] { MarkField.Mark, MarkField.Attribute("WEIGHT") }, " ")</c>.
    /// A field does not change once made.
    /// </remarks>
    public sealed class MarkField
    {
        /// <summary>The position mark: "P1".</summary>
        public static readonly MarkField Mark = new MarkField(MarkFieldKind.Mark, null);

        /// <summary>The name: "BASE PLATE".</summary>
        public static readonly MarkField Name = new MarkField(MarkFieldKind.Name, null);

        /// <summary>The profile: "PL20*200".</summary>
        public static readonly MarkField Profile = new MarkField(MarkFieldKind.Profile, null);

        /// <summary>The material grade: "S355".</summary>
        public static readonly MarkField Material = new MarkField(MarkFieldKind.Material, null);

        /// <summary>The length in whole millimetres: "300".</summary>
        public static readonly MarkField Length = new MarkField(MarkFieldKind.Length, null);

        /// <summary>How many bolts in the group, or parts of the same mark: "4".</summary>
        public static readonly MarkField Count = new MarkField(MarkFieldKind.Count, null);

        /// <summary>A bolt's size: "M20".</summary>
        public static readonly MarkField Diameter = new MarkField(MarkFieldKind.Diameter, null);

        private MarkField(MarkFieldKind kind, string attributeName)
        {
            Kind = kind;
            AttributeName = attributeName;
        }

        /// <summary>Gets what the field shows.</summary>
        public MarkFieldKind Kind { get; }

        /// <summary>Gets the name of the attribute shown, for a field of the kind <see cref="MarkFieldKind.Attribute"/>; null for the others.</summary>
        public string AttributeName { get; }

        /// <summary>
        /// Makes a field showing the value of a named attribute of the object, as <see cref="DrawingObject.Attributes"/>
        /// holds it.
        /// </summary>
        /// <param name="name">The name of the attribute, compared character by character, case and all.</param>
        /// <returns>The field.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        public static MarkField Attribute(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("An attribute field needs the name of the attribute.", nameof(name));
            }

            return new MarkField(MarkFieldKind.Attribute, name);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Kind == MarkFieldKind.Attribute ? "Attribute " + AttributeName : Kind.ToString();
    }
}
