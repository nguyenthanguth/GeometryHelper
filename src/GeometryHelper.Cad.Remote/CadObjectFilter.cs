using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// What <see cref="CadRemoteDocument.GetObjects"/> asks for: the objects of one kind, on one layer, in one space. A member
    /// left null does not narrow the answer, so an empty filter is every object of the drawing's model space and layouts.
    /// </summary>
    /// <example>
    /// <code>
    /// // The lines of layer Steel in model space.
    /// var filter = new CadObjectFilter { Kind = typeof(DxfLine), Layer = "Steel", Layout = "Model" };
    /// </code>
    /// </example>
    public sealed class CadObjectFilter
    {
        private Type _kind;

        /// <summary>
        /// Gets or sets the kind of object, a class of the library's entities: <c>typeof(DxfLine)</c> is the lines,
        /// <c>typeof(DxfDimension)</c> every dimension, rotated, aligned, radial and the rest. Null, the default, is every
        /// kind.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the type is not a <see cref="DxfEntity"/>.</exception>
        public Type Kind
        {
            get => _kind;
            set
            {
                if (value != null && !typeof(DxfEntity).IsAssignableFrom(value))
                {
                    throw new ArgumentException(value.Name + " is not a kind of entity.", nameof(value));
                }

                _kind = value;
            }
        }

        /// <summary>Gets or sets the name of the layer the objects are on, compared without regard to case; null for every layer.</summary>
        public string Layer { get; set; }

        /// <summary>
        /// Gets or sets the space the objects are in: <c>Model</c> for model space, or the name of a layout, compared without
        /// regard to case. Null, the default, is model space and every layout.
        /// </summary>
        public string Layout { get; set; }
    }
}
