using System;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A colour of a HATCH's gradient and where it stands along it, as AutoCAD's <c>GradientColor</c>: the colour (groups 421,
    /// and 63 where the colour is one of AutoCAD's index) and its value (463), 0 at the start of the gradient and 1 at its end.
    /// </summary>
    /// <remarks>
    /// A gradient from red to blue is <c>new DxfGradientColor(DxfColor.FromRgb(255, 0, 0), 0)</c> and
    /// <c>new DxfGradientColor(DxfColor.FromRgb(0, 0, 255), 1)</c>.
    /// </remarks>
    public readonly struct DxfGradientColor
    {
        /// <summary>Initializes a colour of a gradient, as AutoCAD's <c>new GradientColor(color, value)</c>.</summary>
        /// <param name="color">The colour: a true colour, or one of AutoCAD's index.</param>
        /// <param name="value">Where it stands along the gradient, from 0 to 1.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a finite number.</exception>
        public DxfGradientColor(DxfColor color, float value)
        {
            Guard.Finite(value, nameof(value), "The value of a gradient's colour has to be a finite number.");
            Color = color ?? throw new ArgumentNullException(nameof(color));
            Value = value;
        }

        /// <summary>Gets the colour, as AutoCAD's <c>Color</c>.</summary>
        public DxfColor Color { get; }

        /// <summary>Gets where the colour stands along the gradient, as AutoCAD's <c>Value</c>: 0 at its start, 1 at its end.</summary>
        public float Value { get; }
    }
}
