using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The text of one kind of mark: its fields in order, and what stands between two of them.
    /// </summary>
    /// <remarks>
    /// The fields Count, Diameter and Length with the separator " x " give a bolt mark "4 x M20 x 80". A field with nothing
    /// to show is left out with its separator, and a format of no fields marks nothing: a view marks no bolts when
    /// <see cref="MarkContent.Bolt"/> has none. A format does not change once made.
    /// </remarks>
    public sealed class MarkFormat
    {
        private readonly MarkField[] _fields;

        /// <summary>
        /// Makes the format of a kind of mark.
        /// </summary>
        /// <param name="fields">The fields, in the order they are written; none for a kind that is not marked.</param>
        /// <param name="separator">What stands between two fields: " " or " x ".</param>
        /// <exception cref="ArgumentNullException">Thrown when the fields or the separator are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a field is null.</exception>
        public MarkFormat(IEnumerable<MarkField> fields, string separator)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            var list = new List<MarkField>();

            foreach (MarkField field in fields)
            {
                list.Add(field ?? throw new ArgumentException("A field of a mark cannot be null.", nameof(fields)));
            }

            _fields = list.ToArray();
            Separator = separator ?? throw new ArgumentNullException(nameof(separator));
        }

        /// <summary>Gets the fields, in the order they are written.</summary>
        public IReadOnlyList<MarkField> Fields => _fields;

        /// <summary>Gets what stands between two fields.</summary>
        public string Separator { get; }

        /// <inheritdoc/>
        public override string ToString() => string.Join(" | ", Array.ConvertAll(_fields, f => f.ToString()));
    }
}
