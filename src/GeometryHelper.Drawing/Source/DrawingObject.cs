using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One object of the model a drawing is made from: its body in model millimetres, what kind of object it is, and the
    /// texts its marks and the part list show. It knows nothing of where it came from; a bridge fills it from Tekla
    /// Structures, and tests build it in code.
    /// </summary>
    /// <remarks>
    /// An object does not change once made. A plate 300 by 200 by 20 is one object with its body, the kind
    /// <see cref="DrawingObjectKind.Part"/>, an id such as "P-1", and the frame its outline was drawn in: X along its 300,
    /// Y along its 200, Z through its thickness.
    /// <code>
    /// var plate = new DrawingObject("P-1", DrawingObjectKind.Part, body, frame,
    ///     name: "PLATE", mark: "P1", profile: "PL20*200", material: "S355", parentId: "A-1", isMain: true);
    /// </code>
    /// </remarks>
    public sealed class DrawingObject
    {
        private static readonly IReadOnlyDictionary<string, string> NoAttributes =
            new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(StringComparer.Ordinal));

        /// <summary>
        /// Makes an object of the model.
        /// </summary>
        /// <param name="id">The key of the object, unique in its source and the same from one run to the next: Tekla's
        /// GUID, or one given in code. Ids are compared character by character, case and all.</param>
        /// <param name="kind">What the object is.</param>
        /// <param name="solid">The body drawn, in model millimetres.</param>
        /// <param name="frame">The object's own coordinate system: for a member, X along its length and Y up in its
        /// cross-section, as Tekla Structures gives a part's.</param>
        /// <param name="name">The name; null is taken as no name.</param>
        /// <param name="mark">The position mark; null is taken as none.</param>
        /// <param name="profile">The profile, such as "HEA300" or "PL20*200"; null is taken as none.</param>
        /// <param name="material">The material grade, such as "S355"; null is taken as none.</param>
        /// <param name="parentId">The id of the assembly the object belongs to; null or empty where it belongs to none.</param>
        /// <param name="isMain">Whether the object is the main part of its assembly, whose frame a drawing takes by default.</param>
        /// <param name="attributes">Further named values for marks, the title block and the part list; null where there
        /// are none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id or the solid is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id is empty, the frame is not a frame of three unit axes
        /// square to each other, or an attribute has a null name or value or a name given twice.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of
        /// <see cref="DrawingObjectKind"/>.</exception>
        public DrawingObject(
            string id,
            DrawingObjectKind kind,
            GeoSolid3 solid,
            GeoCoordinateSystem3 frame,
            string name = null,
            string mark = null,
            string profile = null,
            string material = null,
            string parentId = null,
            bool isMain = false,
            IEnumerable<KeyValuePair<string, string>> attributes = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (id.Length == 0)
            {
                throw new ArgumentException("An object needs an id.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(DrawingObjectKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind has to be one of DrawingObjectKind.");
            }

            if (!frame.IsValid)
            {
                throw new ArgumentException("The frame has to have three unit axes square to each other.", nameof(frame));
            }

            Id = id;
            Kind = kind;
            Solid = solid ?? throw new ArgumentNullException(nameof(solid));
            Frame = frame;
            Name = name ?? string.Empty;
            Mark = mark ?? string.Empty;
            Profile = profile ?? string.Empty;
            Material = material ?? string.Empty;
            ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
            IsMain = isMain;
            Attributes = attributes == null ? NoAttributes : ReadAttributes(attributes);
        }

        /// <summary>Gets the key of the object, unique in its source.</summary>
        public string Id { get; }

        /// <summary>Gets what the object is.</summary>
        public DrawingObjectKind Kind { get; }

        /// <summary>Gets the body drawn, in model millimetres.</summary>
        public GeoSolid3 Solid { get; }

        /// <summary>Gets the object's own coordinate system.</summary>
        public GeoCoordinateSystem3 Frame { get; }

        /// <summary>Gets the name, empty where there is none.</summary>
        public string Name { get; }

        /// <summary>Gets the position mark, empty where there is none.</summary>
        public string Mark { get; }

        /// <summary>Gets the profile, empty where there is none.</summary>
        public string Profile { get; }

        /// <summary>Gets the material grade, empty where there is none.</summary>
        public string Material { get; }

        /// <summary>Gets the id of the assembly the object belongs to, or null where it belongs to none.</summary>
        public string ParentId { get; }

        /// <summary>Gets whether the object is the main part of its assembly.</summary>
        public bool IsMain { get; }

        /// <summary>
        /// Gets the further named values, read in the order of their names compared character by character, so that a
        /// part list made from them comes out the same every run.
        /// </summary>
        public IReadOnlyDictionary<string, string> Attributes { get; }

        private static IReadOnlyDictionary<string, string> ReadAttributes(IEnumerable<KeyValuePair<string, string>> attributes)
        {
            var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> attribute in attributes)
            {
                if (attribute.Key == null || attribute.Value == null)
                {
                    throw new ArgumentException("An attribute needs a name and a value.", nameof(attributes));
                }

                if (sorted.ContainsKey(attribute.Key))
                {
                    throw new ArgumentException("The attribute \"" + attribute.Key + "\" is given twice.", nameof(attributes));
                }

                sorted.Add(attribute.Key, attribute.Value);
            }

            return new ReadOnlyDictionary<string, string>(sorted);
        }
    }
}
