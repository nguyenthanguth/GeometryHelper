using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// One object of a view: the id the lines it gives carry, and its body in model millimetres.
    /// </summary>
    public readonly struct ProjectionInput
    {
        /// <summary>
        /// Initializes an object of a view.
        /// </summary>
        /// <param name="id">The id of the object, unique among the objects of one view; ids are compared ordinally.</param>
        /// <param name="solid">The body, closed and wound outwards as the library's bodies are; its openings are cut in.</param>
        /// <exception cref="ArgumentException">Thrown when the id is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public ProjectionInput(string id, GeoSolid3 solid)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("An object needs an id.", nameof(id));
            }

            Id = id;
            Solid = solid ?? throw new ArgumentNullException(nameof(solid));
        }

        /// <summary>
        /// Gets the id of the object.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the body of the object.
        /// </summary>
        public GeoSolid3 Solid { get; }

        /// <inheritdoc/>
        public override string ToString() => $"ProjectionInput({Id})";
    }
}
